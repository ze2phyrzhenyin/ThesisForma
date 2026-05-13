import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(SCRIPT_DIR, '..');
const SCHEMAS_DIR = path.join(ROOT, 'schemas');

function readSchemas() {
  return fs
    .readdirSync(SCHEMAS_DIR)
    .filter((name) => name.endsWith('.schema.json'))
    .sort((a, b) => a.localeCompare(b))
    .map((fileName) => {
      const absolutePath = path.join(SCHEMAS_DIR, fileName);
      return {
        fileName,
        stem: fileName.replace(/\.schema\.json$/, ''),
        schema: JSON.parse(fs.readFileSync(absolutePath, 'utf8'))
      };
    });
}

function pascalCase(value) {
  const normalized = value
    .replace(/\.schema\.json$/, '')
    .replace(/[^A-Za-z0-9]+/g, ' ')
    .trim();
  if (!normalized) return 'GeneratedType';
  return normalized
    .split(/\s+/)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join('');
}

function rootTypeName(entry) {
  return pascalCase(entry.schema.title || entry.stem);
}

function definitionTypeName(name) {
  return pascalCase(name);
}

function markdownEscape(value) {
  return String(value ?? '')
    .replace(/\|/g, '\\|')
    .replace(/\n/g, '<br>');
}

function stableString(value) {
  if (value === undefined) return '';
  if (typeof value === 'string') return value;
  return JSON.stringify(value);
}

function localRefName(ref) {
  return definitionTypeName(decodeURIComponent(ref.split('/').at(-1) || ref));
}

function resolveLocalRef(schema, ref) {
  if (!ref.startsWith('#/definitions/')) return undefined;
  const name = decodeURIComponent(ref.slice('#/definitions/'.length));
  return schema.definitions?.[name];
}

function schemaTypeSummary(rootSchema, node) {
  if (!node) return 'unknown';
  if (node.$ref) return localRefName(node.$ref);
  if (node.enum) return `enum(${node.enum.map(stableString).join(', ')})`;
  if (node.const !== undefined) return `const(${stableString(node.const)})`;
  if (node.oneOf) return `oneOf(${node.oneOf.map((child) => schemaTypeSummary(rootSchema, child)).join(' | ')})`;
  if (node.anyOf) return `anyOf(${node.anyOf.map((child) => schemaTypeSummary(rootSchema, child)).join(' | ')})`;
  if (node.allOf) return `allOf(${node.allOf.map((child) => schemaTypeSummary(rootSchema, child)).join(' + ')})`;
  if (node.type === 'array') return `${schemaTypeSummary(rootSchema, node.items)}[]`;
  if (Array.isArray(node.type)) return node.type.join(' | ');
  if (node.type) return node.type;
  if (node.properties || node.patternProperties || node.additionalProperties) return 'object';
  return 'unknown';
}

function collectRequired(node) {
  const required = new Set(node?.required || []);
  for (const child of node?.allOf || []) {
    for (const field of collectRequired(child)) required.add(field);
  }
  return [...required].sort((a, b) => a.localeCompare(b));
}

function collectProperties(rootSchema, node) {
  const rows = [];
  const required = new Set(collectRequired(node));
  for (const child of node?.allOf || []) {
    const target = child.$ref ? resolveLocalRef(rootSchema, child.$ref) : child;
    rows.push(...collectProperties(rootSchema, target));
  }

  for (const [name, property] of Object.entries(node?.properties || {}).sort(([a], [b]) => a.localeCompare(b))) {
    const enumDefault = [
      property.enum ? `enum: ${property.enum.map(stableString).join(', ')}` : '',
      property.default !== undefined ? `default: ${stableString(property.default)}` : ''
    ]
      .filter(Boolean)
      .join('<br>');
    rows.push({
      name,
      type: schemaTypeSummary(rootSchema, property),
      required: required.has(name) ? 'yes' : 'no',
      enumDefault,
      description: property.description || ''
    });
  }

  for (const [pattern, property] of Object.entries(node?.patternProperties || {}).sort(([a], [b]) => a.localeCompare(b))) {
    rows.push({
      name: `/${pattern}/`,
      type: schemaTypeSummary(rootSchema, property),
      required: 'pattern',
      enumDefault: '',
      description: 'Pattern property'
    });
  }

  return rows;
}

function collectDiscriminators(rootSchema, node, location, rows) {
  for (const keyword of ['oneOf', 'anyOf']) {
    for (const child of node?.[keyword] || []) {
      if (!child.$ref) continue;
      const targetName = decodeURIComponent(child.$ref.split('/').at(-1) || child.$ref);
      const target = resolveLocalRef(rootSchema, child.$ref);
      const values = target?.properties?.type?.enum;
      if (values?.length) {
        rows.push({ location, value: values.join(', '), target: definitionTypeName(targetName), keyword });
      }
    }
  }

  for (const [name, definition] of Object.entries(node?.definitions || {})) {
    collectDiscriminators(rootSchema, definition, `#/definitions/${name}`, rows);
  }

  for (const [name, property] of Object.entries(node?.properties || {})) {
    collectDiscriminators(rootSchema, property, `${location}/properties/${name}`, rows);
  }
}

function renderPropertyTable(rootSchema, heading, node) {
  const rows = collectProperties(rootSchema, node);
  if (rows.length === 0) return [];
  const lines = [
    `### ${heading}`,
    '',
    '| Field | Type | Required | Enum / Default | Description |',
    '| --- | --- | --- | --- | --- |'
  ];
  for (const row of rows) {
    lines.push(
      `| \`${markdownEscape(row.name)}\` | ${markdownEscape(row.type)} | ${row.required} | ${markdownEscape(row.enumDefault)} | ${markdownEscape(row.description)} |`
    );
  }
  lines.push('');
  return lines;
}

function renderSchemaDoc(entry) {
  const schema = entry.schema;
  const lines = [
    '<!-- Generated by scripts/generate-schema-docs. Do not edit by hand. -->',
    '',
    `# ${schema.title || rootTypeName(entry)}`,
    '',
    `Source: \`schemas/${entry.fileName}\``,
    '',
    schema.$id ? `Schema ID: \`${schema.$id}\`` : '',
    '',
    schema.description || '',
    '',
    '## Root',
    '',
    `Type: \`${schemaTypeSummary(schema, schema)}\``,
    '',
    `Required fields: ${collectRequired(schema).map((field) => `\`${field}\``).join(', ') || 'none'}`,
    ''
  ].filter((line, index, all) => line !== '' || all[index - 1] !== '');

  lines.push(...renderPropertyTable(schema, 'Root Properties', schema));

  const definitions = Object.entries(schema.definitions || {}).sort(([a], [b]) => a.localeCompare(b));
  if (definitions.length > 0) {
    lines.push('## Definitions', '');
    for (const [name, definition] of definitions) {
      lines.push(...renderPropertyTable(schema, definitionTypeName(name), definition));
    }
  }

  const discriminatorRows = [];
  collectDiscriminators(schema, schema, '#', discriminatorRows);
  if (discriminatorRows.length > 0) {
    lines.push('## Discriminator Mappings', '');
    lines.push('| Location | Keyword | Value | Target |');
    lines.push('| --- | --- | --- | --- |');
    for (const row of discriminatorRows.sort((a, b) => `${a.location}:${a.value}`.localeCompare(`${b.location}:${b.value}`))) {
      lines.push(`| \`${row.location}\` | \`${row.keyword}\` | \`${markdownEscape(row.value)}\` | ${row.target} |`);
    }
    lines.push('');
  }

  return `${lines.join('\n').replace(/\n{3,}/g, '\n\n').trim()}\n`;
}

function literal(value) {
  return JSON.stringify(value);
}

function propertyName(name) {
  return /^[A-Za-z_$][A-Za-z0-9_$]*$/.test(name) ? name : literal(name);
}

function typeNeedsParens(type) {
  return type.includes(' | ') || type.includes(' & ');
}

function arrayOf(type) {
  return `${typeNeedsParens(type) ? `(${type})` : type}[]`;
}

function tsType(rootSchema, node) {
  if (!node) return 'unknown';
  if (node.$ref) return localRefName(node.$ref);
  if (node.const !== undefined) return literal(node.const);
  if (node.enum) return node.enum.map(literal).join(' | ') || 'never';
  if (node.oneOf) return node.oneOf.map((child) => tsType(rootSchema, child)).join(' | ');
  if (node.anyOf) return node.anyOf.map((child) => tsType(rootSchema, child)).join(' | ');
  if (node.allOf) return node.allOf.map((child) => tsType(rootSchema, child)).join(' & ');

  const schemaType = Array.isArray(node.type) ? undefined : node.type;
  if (schemaType === 'string') return 'string';
  if (schemaType === 'number' || schemaType === 'integer') return 'number';
  if (schemaType === 'boolean') return 'boolean';
  if (schemaType === 'null') return 'null';
  if (schemaType === 'array') return arrayOf(tsType(rootSchema, node.items || {}));
  if (schemaType === 'object' || node.properties || node.patternProperties || node.additionalProperties) {
    return inlineObjectType(rootSchema, node, 0);
  }
  if (Array.isArray(node.type)) {
    return node.type.map((type) => tsType(rootSchema, { ...node, type })).join(' | ');
  }
  return 'unknown';
}

function inlineObjectType(rootSchema, node, depth) {
  const required = new Set(collectRequired(node));
  const indent = '  '.repeat(depth + 1);
  const closeIndent = '  '.repeat(depth);
  const lines = ['{'];
  for (const [name, property] of Object.entries(node.properties || {}).sort(([a], [b]) => a.localeCompare(b))) {
    lines.push(`${indent}${propertyName(name)}${required.has(name) ? '' : '?'}: ${tsType(rootSchema, property)};`);
  }

  const patternTypes = Object.values(node.patternProperties || {}).map((property) => tsType(rootSchema, property));
  if (patternTypes.length > 0) {
    lines.push(`${indent}[key: string]: ${[...new Set(patternTypes)].join(' | ')} | undefined;`);
  } else if (node.additionalProperties === true) {
    lines.push(`${indent}[key: string]: unknown;`);
  } else if (typeof node.additionalProperties === 'object') {
    lines.push(`${indent}[key: string]: ${tsType(rootSchema, node.additionalProperties)};`);
  }

  lines.push(`${closeIndent}}`);
  return lines.join('\n');
}

function declaration(rootSchema, name, node) {
  if ((node.type === 'object' || node.properties || node.patternProperties || node.additionalProperties) && !node.oneOf && !node.anyOf && !node.allOf) {
    return `export interface ${name} ${inlineObjectType(rootSchema, node, 0)}`;
  }

  return `export type ${name} = ${tsType(rootSchema, node)};`;
}

function renderTypes(entries) {
  const lines = [
    '/* eslint-disable */',
    '// Generated by scripts/generate-web-types. Do not edit by hand.',
    ''
  ];
  const rootAliases = [];

  for (const entry of entries) {
    const schema = entry.schema;
    const namespace = `${rootTypeName(entry)}Schema`;
    const rootName = rootTypeName(entry);
    lines.push(`export namespace ${namespace} {`);
    for (const [name, definition] of Object.entries(schema.definitions || {}).sort(([a], [b]) => a.localeCompare(b))) {
      lines.push(indentBlock(declaration(schema, definitionTypeName(name), definition), 1), '');
    }
    lines.push(indentBlock(declaration(schema, rootName, schema), 1));
    lines.push('}', '');
    rootAliases.push(`export type ${rootName} = ${namespace}.${rootName};`);
  }

  lines.push(...rootAliases.sort((a, b) => a.localeCompare(b)), '');
  return `${lines.join('\n').replace(/\n{3,}/g, '\n\n').trim()}\n`;
}

function indentBlock(value, depth) {
  const prefix = '  '.repeat(depth);
  return value
    .split('\n')
    .map((line) => (line ? prefix + line : line))
    .join('\n');
}

function renderDocsIndex(entries) {
  const lines = [
    '<!-- Generated by scripts/generate-schema-docs. Do not edit by hand. -->',
    '',
    '# Generated Schema Documentation',
    '',
    '| Schema | Source | Generated Doc |',
    '| --- | --- | --- |'
  ];
  for (const entry of entries) {
    lines.push(`| ${entry.schema.title || rootTypeName(entry)} | \`schemas/${entry.fileName}\` | [${entry.stem}](./${entry.stem}.md) |`);
  }
  lines.push('');
  return lines.join('\n');
}

function writeOrCheck(outputs, check) {
  const stale = [];
  for (const [relativePath, content] of outputs) {
    const absolutePath = path.join(ROOT, relativePath);
    if (check) {
      const current = fs.existsSync(absolutePath) ? fs.readFileSync(absolutePath, 'utf8') : null;
      if (current !== content) stale.push(relativePath);
      continue;
    }

    fs.mkdirSync(path.dirname(absolutePath), { recursive: true });
    fs.writeFileSync(absolutePath, content);
  }

  if (stale.length > 0) {
    console.error(`Generated output is stale:\n${stale.map((file) => `- ${file}`).join('\n')}`);
    process.exitCode = 1;
  } else if (check) {
    console.log('Generated output is up to date.');
  }
}

export function generateSchemaDocs({ check = false } = {}) {
  const entries = readSchemas();
  const outputs = new Map();
  outputs.set('docs/generated/schemas.md', renderDocsIndex(entries));
  for (const entry of entries) {
    outputs.set(`docs/generated/${entry.stem}.md`, renderSchemaDoc(entry));
  }
  writeOrCheck(outputs, check);
}

export function generateWebTypes({ check = false } = {}) {
  const entries = readSchemas();
  const outputs = new Map();
  outputs.set('web/src/types/generated/schemas.ts', renderTypes(entries));
  outputs.set('web/src/types/generated/index.ts', "export * from './schemas';\n");
  writeOrCheck(outputs, check);
}

export function hasCheckFlag(argv) {
  return argv.includes('--check');
}
