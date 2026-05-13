import { useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import type { TemplateLayoutBlock, TemplateMetadataFieldBlock, TemplatePackage } from '@/types';
import {
  METADATA_SOURCE_PATHS,
  PAGE_TEMPLATE_BLOCK_TYPES,
  type PageTemplateBlockType,
  cloneLayoutBlock,
  createLayoutBlock,
  layoutBlockPreview,
  layoutBlockSummary,
  metadataFieldTarget
} from './pageTemplateBlocks';
import styles from '@/pages/TemplateEditorPage.module.css';

interface Props {
  template: TemplatePackage;
  update(updater: (draft: TemplatePackage) => void): void;
}

export function PageTemplatesPanel({ template, update }: Props) {
  const [expanded, setExpanded] = useState<string | null>(null);
  const [newType, setNewType] = useState<PageTemplateBlockType>('text');
  const variableDefaults = useMemo(
    () =>
      Object.fromEntries(
        (template.variables ?? []).map((variable) => [
          variable.name,
          String(variable.defaultValue ?? `{{variables.${variable.name}}}`)
        ])
      ),
    [template.variables]
  );

  return (
    <section className={styles.panel}>
      <header className={styles.panelHeader}>
        <h2>Page Templates</h2>
        <button
          type="button"
          onClick={() =>
            update((t) => {
              t.pageTemplates = [
                ...(t.pageTemplates ?? []),
                {
                  id: `page-${(t.pageTemplates ?? []).length + 1}`,
                  targetSectionType: 'cover',
                  insertPosition: 'replaceSectionContent',
                  blocks: [createLayoutBlock('text')]
                }
              ];
            })
          }
        >
          ＋ 新增页面模板
        </button>
      </header>
      {(template.pageTemplates ?? []).length === 0 ? (
        <div className={styles.empty}>暂无页面模板。</div>
      ) : (
        <div className={styles.pageTemplateList}>
          {(template.pageTemplates ?? []).map((pageTemplate, templateIndex) => (
            <div key={pageTemplate.id} className={styles.pageTemplateItem}>
              <div className={styles.pageTemplateMeta}>
                <Field label="id">
                  <input
                    value={pageTemplate.id}
                    onChange={(e) =>
                      update((t) => {
                        if (t.pageTemplates?.[templateIndex]) t.pageTemplates[templateIndex].id = e.target.value;
                      })
                    }
                  />
                </Field>
                <Field label="targetSectionType">
                  <select
                    value={pageTemplate.targetSectionType}
                    onChange={(e) =>
                      update((t) => {
                        if (t.pageTemplates?.[templateIndex]) t.pageTemplates[templateIndex].targetSectionType = e.target.value;
                      })
                    }
                  >
                    {['cover', 'declaration', 'abstract', 'toc', 'body', 'appendix'].map((kind) => (
                      <option key={kind} value={kind}>
                        {kind}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="insertPosition">
                  <select
                    value={pageTemplate.insertPosition}
                    onChange={(e) =>
                      update((t) => {
                        if (t.pageTemplates?.[templateIndex]) t.pageTemplates[templateIndex].insertPosition = e.target.value;
                      })
                    }
                  >
                    {['beforeSection', 'afterSection', 'replaceSectionContent'].map((value) => (
                      <option key={value} value={value}>
                        {value}
                      </option>
                    ))}
                  </select>
                </Field>
              </div>
              <div className={styles.blockAddRow}>
                <select
                  aria-label="新增页面模板元素类型"
                  value={newType}
                  onChange={(e) => setNewType(e.target.value as PageTemplateBlockType)}
                >
                  {PAGE_TEMPLATE_BLOCK_TYPES.map((type) => (
                    <option key={type} value={type}>
                      {type}
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  onClick={() =>
                    update((t) => {
                      t.pageTemplates?.[templateIndex]?.blocks.push(createLayoutBlock(newType));
                    })
                  }
                >
                  ＋ 新增元素
                </button>
              </div>
              <div className={styles.layoutBlocks}>
                {pageTemplate.blocks.map((block, blockIndex) => (
                  <LayoutBlockEditor
                    key={`${pageTemplate.id}-${blockIndex}`}
                    block={block}
                    expanded={expanded === `${templateIndex}:${blockIndex}`}
                    summary={layoutBlockSummary(block)}
                    preview={layoutBlockPreview(block, variableDefaults)}
                    onToggle={() =>
                      setExpanded((current) => (current === `${templateIndex}:${blockIndex}` ? null : `${templateIndex}:${blockIndex}`))
                    }
                    onChange={(patch) =>
                      update((t) => {
                        const item = t.pageTemplates?.[templateIndex]?.blocks[blockIndex];
                        if (item) Object.assign(item, patch);
                      })
                    }
                    onChangeMetadataField={(patch) =>
                      update((t) => {
                        const item = t.pageTemplates?.[templateIndex]?.blocks[blockIndex];
                        if (item?.type === 'metadataField') Object.assign(item, patch);
                      })
                    }
                    onMove={(direction) =>
                      update((t) => {
                        const blocks = t.pageTemplates?.[templateIndex]?.blocks;
                        if (!blocks) return;
                        const target = blockIndex + direction;
                        if (target < 0 || target >= blocks.length) return;
                        const [item] = blocks.splice(blockIndex, 1);
                        if (item) blocks.splice(target, 0, item);
                      })
                    }
                    onDuplicate={() =>
                      update((t) => {
                        t.pageTemplates?.[templateIndex]?.blocks.splice(blockIndex + 1, 0, cloneLayoutBlock(block));
                      })
                    }
                    onDelete={() =>
                      update((t) => {
                        t.pageTemplates?.[templateIndex]?.blocks.splice(blockIndex, 1);
                      })
                    }
                  />
                ))}
              </div>
              <div className={styles.templatePreview} aria-label={`${pageTemplate.id} 结构化预览`}>
                {pageTemplate.blocks.map((block, index) => (
                  <pre key={index}>{layoutBlockPreview(block, variableDefaults)}</pre>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function LayoutBlockEditor({
  block,
  expanded,
  summary,
  preview,
  onToggle,
  onChange,
  onChangeMetadataField,
  onMove,
  onDuplicate,
  onDelete
}: {
  block: TemplateLayoutBlock;
  expanded: boolean;
  summary: string;
  preview: string;
  onToggle(): void;
  onChange(patch: Partial<TemplateLayoutBlock>): void;
  onChangeMetadataField(patch: Partial<TemplateMetadataFieldBlock>): void;
  onMove(direction: -1 | 1): void;
  onDuplicate(): void;
  onDelete(): void;
}) {
  return (
    <div className={styles.layoutBlock} data-expanded={expanded}>
      <button type="button" className={styles.blockSummary} onClick={onToggle}>
        <code>{block.type}</code>
        <span>{summary}</span>
      </button>
      <div className={styles.blockTools}>
        <button type="button" onClick={() => onMove(-1)} aria-label="上移元素">
          ↑
        </button>
        <button type="button" onClick={() => onMove(1)} aria-label="下移元素">
          ↓
        </button>
        <button type="button" onClick={onDuplicate}>
          复制
        </button>
        <button type="button" onClick={onDelete}>
          删除
        </button>
      </div>
      {expanded && (
        <div className={styles.blockDetails}>
          <LayoutBlockFields block={block} onChange={onChange} onChangeMetadataField={onChangeMetadataField} />
          <pre className={styles.blockPreview}>{preview}</pre>
        </div>
      )}
    </div>
  );
}

function LayoutBlockFields({
  block,
  onChange,
  onChangeMetadataField
}: {
  block: TemplateLayoutBlock;
  onChange(patch: Partial<TemplateLayoutBlock>): void;
  onChangeMetadataField(patch: Partial<TemplateMetadataFieldBlock>): void;
}) {
  switch (block.type) {
    case 'spacer':
      return (
        <Field label="heightCm">
          <input type="number" min={0} max={20} step={0.1} value={block.heightCm} onChange={(e) => onChange({ heightCm: numberOrUndefined(e.target.value) ?? 0 } as Partial<TemplateLayoutBlock>)} />
        </Field>
      );
    case 'text':
      return (
        <div className={styles.blockFieldGrid}>
          <Field label="value" wide>
            <textarea rows={2} value={block.value} onChange={(e) => onChange({ value: e.target.value } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="style">
            <input value={block.style ?? ''} onChange={(e) => onChange({ style: e.target.value } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <AlignmentField value={block.alignment} onChange={(alignment) => onChange({ alignment } as Partial<TemplateLayoutBlock>)} />
          <Field label="spacingBeforePt">
            <input type="number" min={0} step={1} value={block.spacingBeforePt ?? ''} onChange={(e) => onChange({ spacingBeforePt: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="spacingAfterPt">
            <input type="number" min={0} step={1} value={block.spacingAfterPt ?? ''} onChange={(e) => onChange({ spacingAfterPt: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="font sizePt">
            <input
              type="number"
              min={1}
              max={72}
              step={0.5}
              value={block.fontOverride?.sizePt ?? ''}
              onChange={(e) =>
                onChange({ fontOverride: { ...(block.fontOverride ?? {}), sizePt: numberOrUndefined(e.target.value) } } as Partial<TemplateLayoutBlock>)
              }
            />
          </Field>
        </div>
      );
    case 'metadataField':
      return <MetadataFieldEditor field={block} onChange={onChangeMetadataField} />;
    case 'image':
      return (
        <div className={styles.blockFieldGrid}>
          <Field label="assetId">
            <input value={block.assetId} onChange={(e) => onChange({ assetId: e.target.value } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="widthCm">
            <input type="number" min={0} step={0.1} value={block.widthCm ?? ''} onChange={(e) => onChange({ widthCm: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="heightCm">
            <input type="number" min={0} step={0.1} value={block.heightCm ?? ''} onChange={(e) => onChange({ heightCm: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <AlignmentField value={block.alignment} onChange={(alignment) => onChange({ alignment } as Partial<TemplateLayoutBlock>)} />
        </div>
      );
    case 'fieldTable':
      return (
        <div className={styles.blockFieldGrid}>
          <Field label="columns">
            <input type="number" min={1} max={6} value={block.columns ?? ''} onChange={(e) => onChange({ columns: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="borderMode">
            <select value={block.borderMode ?? 'none'} onChange={(e) => onChange({ borderMode: e.target.value } as Partial<TemplateLayoutBlock>)}>
              {['none', 'full', 'bottomLine', 'custom'].map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
          </Field>
          <Field label="labelColumnWidthCm">
            <input type="number" min={0} step={0.1} value={block.labelColumnWidthCm ?? ''} onChange={(e) => onChange({ labelColumnWidthCm: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="valueColumnWidthCm">
            <input type="number" min={0} step={0.1} value={block.valueColumnWidthCm ?? ''} onChange={(e) => onChange({ valueColumnWidthCm: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="rows" wide>
            <textarea
              rows={4}
              value={block.rows.map((row) => row.map((field) => `${field.label}:${metadataFieldTarget(field)}`).join(' | ')).join('\n')}
              onChange={(e) =>
                onChange({
                  rows: e.target.value.split('\n').map((line) =>
                    line.split('|').map((cell) => {
                      const [label = '字段', target = 'metadata.title'] = cell.split(':').map((part) => part.trim());
                      return target.startsWith('variables.')
                        ? { type: 'metadataField', label, variableName: target.replace(/^variables\./, ''), layout: 'tableRow' }
                        : { type: 'metadataField', label, sourcePath: target || 'metadata.title', layout: 'tableRow' };
                    })
                  )
                } as Partial<TemplateLayoutBlock>)
              }
            />
          </Field>
        </div>
      );
    case 'declarationText':
      return (
        <div className={styles.blockFieldGrid}>
          <Field label="paragraphs" wide>
            <textarea rows={4} value={block.paragraphs.join('\n')} onChange={(e) => onChange({ paragraphs: e.target.value.split('\n') } as Partial<TemplateLayoutBlock>)} />
          </Field>
        </div>
      );
    case 'pageBreak':
      return <div className={styles.empty}>分页元素没有额外字段。</div>;
    case 'rule':
      return (
        <div className={styles.blockFieldGrid}>
          <Field label="thicknessPt">
            <input type="number" min={0.25} max={12} step={0.25} value={block.thicknessPt ?? ''} onChange={(e) => onChange({ thicknessPt: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="color">
            <input value={block.color ?? ''} onChange={(e) => onChange({ color: e.target.value } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <AlignmentField value={block.alignment} onChange={(alignment) => onChange({ alignment } as Partial<TemplateLayoutBlock>)} />
          <Field label="spacingBeforePt">
            <input type="number" min={0} step={1} value={block.spacingBeforePt ?? ''} onChange={(e) => onChange({ spacingBeforePt: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
          <Field label="spacingAfterPt">
            <input type="number" min={0} step={1} value={block.spacingAfterPt ?? ''} onChange={(e) => onChange({ spacingAfterPt: numberOrUndefined(e.target.value) } as Partial<TemplateLayoutBlock>)} />
          </Field>
        </div>
      );
  }
}

function MetadataFieldEditor({
  field,
  onChange
}: {
  field: TemplateMetadataFieldBlock;
  onChange(patch: Partial<TemplateMetadataFieldBlock>): void;
}) {
  return (
    <div className={styles.blockFieldGrid}>
      <Field label="label">
        <input value={field.label} onChange={(e) => onChange({ label: e.target.value })} />
      </Field>
      <Field label="sourcePath">
        <input list="metadata-source-paths" value={field.sourcePath ?? ''} onChange={(e) => onChange({ sourcePath: e.target.value })} />
        <datalist id="metadata-source-paths">
          {METADATA_SOURCE_PATHS.map((path) => (
            <option key={path} value={path} />
          ))}
        </datalist>
      </Field>
      <Field label="variableName">
        <input value={field.variableName ?? ''} onChange={(e) => onChange({ variableName: e.target.value })} />
      </Field>
      <Field label="layout">
        <select value={field.layout ?? 'labelValueLine'} onChange={(e) => onChange({ layout: e.target.value })}>
          {['inline', 'labelValueLine', 'tableRow'].map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>
      </Field>
      <AlignmentField value={field.alignment} onChange={(alignment) => onChange({ alignment })} />
      <label className={styles.checkbox}>
        <input type="checkbox" checked={field.underline === true} onChange={(e) => onChange({ underline: e.target.checked })} />
        underline
      </label>
      <Field label="valueTemplate" wide>
        <input value={field.valueTemplate ?? ''} onChange={(e) => onChange({ valueTemplate: e.target.value })} />
      </Field>
    </div>
  );
}

function AlignmentField({
  value,
  onChange
}: {
  value: string | undefined;
  onChange(value: string | undefined): void;
}) {
  return (
    <Field label="alignment">
      <select value={value ?? ''} onChange={(e) => onChange(e.target.value || undefined)}>
        <option value="">默认</option>
        {['left', 'center', 'right', 'both'].map((item) => (
          <option key={item} value={item}>
            {item}
          </option>
        ))}
      </select>
    </Field>
  );
}

function Field({
  label,
  required,
  wide,
  children
}: {
  label: string;
  required?: boolean;
  wide?: boolean;
  children: ReactNode;
}) {
  return (
    <label className={styles.field} data-wide={wide === true}>
      <span>
        {label}
        {required && <b>*</b>}
      </span>
      {children}
    </label>
  );
}

function numberOrUndefined(value: string): number | undefined {
  const n = Number(value);
  return Number.isFinite(n) ? n : undefined;
}
