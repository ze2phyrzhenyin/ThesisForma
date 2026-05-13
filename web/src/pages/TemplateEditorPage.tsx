import { Suspense, lazy, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Brand } from '@/components/Brand';
import type {
  ApiIssue,
  TemplateAsset,
  TemplatePackage,
  TemplateVariable
} from '@/types';
import {
  cleanTemplatePackage,
  createBlankTemplatePackage,
  createDefaultFormatSpec,
  exportTemplatePackage,
  parseTemplatePackageJson,
  validateTemplatePackage
} from '@/templates/templateContract';
import styles from './TemplateEditorPage.module.css';

const PageTemplatesPanel = lazy(() =>
  import('@/templates/TemplatePageTemplatesPanel').then((module) => ({ default: module.PageTemplatesPanel }))
);

const VARIABLE_TYPES: TemplateVariable['type'][] = [
  'string',
  'multilineText',
  'date',
  'number',
  'boolean',
  'enum',
  'image',
  'richText'
];

export function TemplateEditorPage() {
  const [template, setTemplate] = useState<TemplatePackage>(() => createBlankTemplatePackage());
  const [message, setMessage] = useState<string | null>(null);
  const issues = useMemo(() => validateTemplatePackage(template), [template]);
  const errors = issues.filter((issue) => issue.severity === 'error');

  const update = (updater: (draft: TemplatePackage) => void) => {
    setTemplate((current) => {
      const next = structuredClone(current);
      updater(next);
      return next;
    });
  };

  const importTemplate = async (file: File) => {
    setMessage(null);
    const result = parseTemplatePackageJson(await file.text());
    if (!result.ok || !result.template) {
      const first = result.issues.find((issue) => issue.severity === 'error') ?? result.issues[0];
      setMessage(first?.message ?? '模板导入失败');
      return;
    }
    setTemplate(result.template);
    const warningCount = result.issues.filter((issue) => issue.severity === 'warning').length;
    setMessage(warningCount ? `已导入，包含 ${warningCount} 个警告。` : '已导入模板包。');
  };

  const onExport = () => {
    if (errors.length > 0) {
      const proceed = window.confirm(`模板仍有 ${errors.length} 个错误。是否仍然导出草稿 JSON？`);
      if (!proceed) return;
    }
    exportTemplatePackage(cleanTemplatePackage(template));
  };

  return (
    <div className={styles.page}>
      <header className={styles.topbar}>
        <Brand />
        <nav className={styles.nav}>
          <a href="/">首页</a>
          <a href="/templates">模板库</a>
        </nav>
      </header>

      <main className={styles.main}>
        <header className={styles.header}>
          <div>
            <h1 className={styles.title}>模板包编辑器</h1>
            <p className={styles.subtitle}>编辑 TemplatePackage JSON 的核心结构，不连接后端，不执行模板解析。</p>
          </div>
          <div className={styles.headerActions}>
            <label className={styles.importBtn}>
              导入 JSON
              <input
                type="file"
                accept="application/json,.json"
                hidden
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) void importTemplate(file);
                }}
              />
            </label>
            <button type="button" className={styles.exportBtn} onClick={onExport}>
              导出模板 JSON
            </button>
          </div>
        </header>

        {message && <div className={styles.message}>{message}</div>}

        <div className={styles.layout}>
          <section className={styles.panel}>
            <h2>基本信息</h2>
            <div className={styles.grid}>
              <Field label="templateSchemaVersion">
                <input value={template.templateSchemaVersion} disabled />
              </Field>
              <Field label="id" required>
                <input value={template.id} onChange={(e) => update((t) => (t.id = e.target.value))} />
              </Field>
              <Field label="name" required>
                <input value={template.name} onChange={(e) => update((t) => (t.name = e.target.value))} />
              </Field>
              <Field label="version" required>
                <input value={template.version} onChange={(e) => update((t) => (t.version = e.target.value))} />
              </Field>
              <Field label="locale" required>
                <input value={template.locale} onChange={(e) => update((t) => (t.locale = e.target.value))} />
              </Field>
              <Field label="school">
                <input value={template.school ?? ''} onChange={(e) => update((t) => (t.school = e.target.value))} />
              </Field>
              <Field label="college">
                <input value={template.college ?? ''} onChange={(e) => update((t) => (t.college = e.target.value))} />
              </Field>
              <Field label="degreeType">
                <input value={template.degreeType ?? ''} onChange={(e) => update((t) => (t.degreeType = e.target.value))} />
              </Field>
              <Field label="description" wide>
                <textarea
                  rows={3}
                  value={template.description ?? ''}
                  onChange={(e) => update((t) => (t.description = e.target.value))}
                />
              </Field>
              <Field label="tags" wide>
                <input
                  value={(template.tags ?? []).join(', ')}
                  placeholder="tag-a, tag-b"
                  onChange={(e) =>
                    update((t) => {
                      t.tags = e.target.value.split(',').map((tag) => tag.trim()).filter(Boolean);
                    })
                  }
                />
              </Field>
              <Field label="formatSpecRef" wide>
                <input
                  value={template.formatSpecRef ?? ''}
                  placeholder="format-spec.json"
                  onChange={(e) =>
                    update((t) => {
                      if (e.target.value.trim()) t.formatSpecRef = e.target.value.trim();
                      else delete t.formatSpecRef;
                    })
                  }
                />
              </Field>
            </div>
          </section>

          <section className={styles.panel}>
            <header className={styles.panelHeader}>
              <h2>变量</h2>
              <button
                type="button"
                onClick={() =>
                  update((t) => {
                    t.variables = [
                      ...(t.variables ?? []),
                      { name: `var${(t.variables ?? []).length + 1}`, label: '新变量', type: 'string' }
                    ];
                  })
                }
              >
                ＋ 新增变量
              </button>
            </header>
            {(template.variables ?? []).length === 0 ? (
              <div className={styles.empty}>暂无变量。</div>
            ) : (
              <div className={styles.variableList}>
                {(template.variables ?? []).map((variable, index) => (
                  <div key={`${variable.name}-${index}`} className={styles.variableItem}>
                    <input
                      value={variable.name}
                      placeholder="name"
                      onChange={(e) => updateVariable(update, index, { name: e.target.value })}
                    />
                    <input
                      value={variable.label ?? ''}
                      placeholder="label"
                      onChange={(e) => updateVariable(update, index, { label: e.target.value })}
                    />
                    <select
                      value={variable.type}
                      onChange={(e) => updateVariable(update, index, { type: e.target.value as TemplateVariable['type'] })}
                    >
                      {VARIABLE_TYPES.map((type) => (
                        <option key={type} value={type}>
                          {type}
                        </option>
                      ))}
                    </select>
                    <label className={styles.checkbox}>
                      <input
                        type="checkbox"
                        checked={variable.required === true}
                        onChange={(e) => updateVariable(update, index, { required: e.target.checked })}
                      />
                      必填
                    </label>
                    <input
                      value={variable.sourcePath ?? ''}
                      placeholder="metadata.title"
                      onChange={(e) => updateVariable(update, index, { sourcePath: e.target.value })}
                    />
                    <input
                      value={String(variable.defaultValue ?? '')}
                      placeholder="default"
                      onChange={(e) => updateVariable(update, index, { defaultValue: e.target.value })}
                    />
                    <button
                      type="button"
                      className={styles.removeBtn}
                      onClick={() =>
                        update((t) => {
                          t.variables?.splice(index, 1);
                        })
                      }
                    >
                      删除
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>

          <AssetsPanel template={template} update={update} />
          <FormatSpecPanel template={template} update={update} />
          <Suspense
            fallback={
              <section className={styles.panel}>
                <h2>Page Templates</h2>
                <div className={styles.empty}>正在加载页面模板编辑器…</div>
              </section>
            }
          >
            <PageTemplatesPanel template={template} update={update} />
          </Suspense>
          <ValidationPanel issues={issues} />
        </div>
      </main>
    </div>
  );
}

function FormatSpecPanel({
  template,
  update
}: {
  template: TemplatePackage;
  update(updater: (draft: TemplatePackage) => void): void;
}) {
  const formatSpec = template.formatSpec;
  return (
    <section className={styles.panel}>
      <header className={styles.panelHeader}>
        <h2>FormatSpec 基础项</h2>
        {!formatSpec && (
          <button type="button" onClick={() => update((t) => (t.formatSpec = createDefaultFormatSpec()))}>
            嵌入基础 FormatSpec
          </button>
        )}
      </header>
      {!formatSpec ? (
        <div className={styles.empty}>当前模板使用 formatSpecRef 或继承；前端不读取目录文件。</div>
      ) : (
        <div className={styles.grid}>
          {(['topMarginCm', 'bottomMarginCm', 'leftMarginCm', 'rightMarginCm', 'headerDistanceCm', 'footerDistanceCm'] as const).map((key) => (
            <Field key={key} label={key}>
              <input
                type="number"
                min={0}
                step={0.1}
                value={formatSpec.pageSetup?.[key] ?? ''}
                onChange={(e) =>
                  update((t) => {
                    t.formatSpec ??= createDefaultFormatSpec();
                    t.formatSpec.pageSetup ??= {};
                    t.formatSpec.pageSetup[key] = numberOrUndefined(e.target.value);
                  })
                }
              />
            </Field>
          ))}
          <Field label="paperSize">
            <select
              value={formatSpec.pageSetup?.paperSize ?? 'a4'}
              onChange={(e) =>
                update((t) => {
                  t.formatSpec ??= createDefaultFormatSpec();
                  t.formatSpec.pageSetup ??= {};
                  t.formatSpec.pageSetup.paperSize = e.target.value;
                })
              }
            >
              <option value="a4">a4</option>
              <option value="letter">letter</option>
            </select>
          </Field>
          <Field label="eastAsia font">
            <input
              value={formatSpec.defaultFont?.eastAsia ?? ''}
              onChange={(e) =>
                update((t) => {
                  t.formatSpec ??= createDefaultFormatSpec();
                  t.formatSpec.defaultFont ??= {};
                  t.formatSpec.defaultFont.eastAsia = e.target.value;
                })
              }
            />
          </Field>
          <Field label="latin font">
            <input
              value={formatSpec.defaultFont?.latin ?? ''}
              onChange={(e) =>
                update((t) => {
                  t.formatSpec ??= createDefaultFormatSpec();
                  t.formatSpec.defaultFont ??= {};
                  t.formatSpec.defaultFont.latin = e.target.value;
                })
              }
            />
          </Field>
          <Field label="sizePt">
            <input
              type="number"
              min={1}
              max={72}
              step={0.5}
              value={formatSpec.defaultFont?.sizePt ?? ''}
              onChange={(e) =>
                update((t) => {
                  t.formatSpec ??= createDefaultFormatSpec();
                  t.formatSpec.defaultFont ??= {};
                  t.formatSpec.defaultFont.sizePt = numberOrUndefined(e.target.value);
                })
              }
            />
          </Field>
          <Field label="lineSpacing">
            <input
              type="number"
              min={0.5}
              max={4}
              step={0.1}
              value={formatSpec.bodyParagraph?.lineSpacingMultiple ?? ''}
              onChange={(e) =>
                update((t) => {
                  t.formatSpec ??= createDefaultFormatSpec();
                  t.formatSpec.bodyParagraph ??= {};
                  t.formatSpec.bodyParagraph.lineSpacingMultiple = numberOrUndefined(e.target.value);
                })
              }
            />
          </Field>
          <Field label="exactLineSpacing">
            <input
              type="number"
              min={1}
              max={120}
              step={0.5}
              value={formatSpec.bodyParagraph?.lineSpacingExactPt ?? ''}
              onChange={(e) =>
                update((t) => {
                  t.formatSpec ??= createDefaultFormatSpec();
                  t.formatSpec.bodyParagraph ??= {};
                  t.formatSpec.bodyParagraph.lineSpacingExactPt = numberOrUndefined(e.target.value);
                })
              }
            />
          </Field>
        </div>
      )}
    </section>
  );
}

function AssetsPanel({
  template,
  update
}: {
  template: TemplatePackage;
  update(updater: (draft: TemplatePackage) => void): void;
}) {
  return (
    <section className={styles.panel}>
      <header className={styles.panelHeader}>
        <h2>Assets</h2>
        <button
          type="button"
          onClick={() =>
            update((t) => {
              t.assets = [
                ...(t.assets ?? []),
                { id: `asset${(t.assets ?? []).length + 1}`, type: 'image', path: 'assets/image.png', contentType: 'image/png' }
              ];
            })
          }
        >
          ＋ 新增 asset
        </button>
      </header>
      {(template.assets ?? []).length === 0 ? (
        <div className={styles.empty}>暂无 assets。图片元素引用 assetId 时，需要在这里或导入 JSON 中提供对应 asset。</div>
      ) : (
        <div className={styles.assetList}>
          {(template.assets ?? []).map((asset, index) => (
            <div key={`${asset.id}-${index}`} className={styles.assetItem}>
              <input value={asset.id} placeholder="id" onChange={(e) => updateAsset(update, index, { id: e.target.value })} />
              <select value={asset.type} onChange={(e) => updateAsset(update, index, { type: e.target.value })}>
                {['image', 'font', 'staticDocxFragment', 'text'].map((type) => (
                  <option key={type} value={type}>
                    {type}
                  </option>
                ))}
              </select>
              <input value={asset.path} placeholder="assets/logo.png" onChange={(e) => updateAsset(update, index, { path: e.target.value })} />
              <input value={asset.contentType} placeholder="image/png" onChange={(e) => updateAsset(update, index, { contentType: e.target.value })} />
              <input value={asset.description ?? ''} placeholder="description" onChange={(e) => updateAsset(update, index, { description: e.target.value })} />
              <label className={styles.checkbox}>
                <input type="checkbox" checked={asset.required === true} onChange={(e) => updateAsset(update, index, { required: e.target.checked })} />
                required
              </label>
              <button
                type="button"
                className={styles.removeBtn}
                onClick={() =>
                  update((t) => {
                    t.assets?.splice(index, 1);
                  })
                }
              >
                删除
              </button>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function ValidationPanel({ issues }: { issues: ApiIssue[] }) {
  return (
    <aside className={styles.validation}>
      <h2>模板校验</h2>
      {issues.length === 0 ? (
        <div className={styles.empty}>未发现问题。</div>
      ) : (
        <ul>
          {issues.map((issue, index) => (
            <li key={`${issue.code}-${index}`} data-severity={issue.severity}>
              <strong>{issue.severity}</strong>
              <span>{issue.message}</span>
              {issue.path && <code>{issue.path}</code>}
            </li>
          ))}
        </ul>
      )}
    </aside>
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

function updateVariable(
  update: (updater: (draft: TemplatePackage) => void) => void,
  index: number,
  patch: Partial<TemplateVariable>
) {
  update((template) => {
    const variable = template.variables?.[index];
    if (variable) Object.assign(variable, patch);
  });
}

function updateAsset(
  update: (updater: (draft: TemplatePackage) => void) => void,
  index: number,
  patch: Partial<TemplateAsset>
) {
  update((template) => {
    const asset = template.assets?.[index];
    if (asset) Object.assign(asset, patch);
  });
}

function numberOrUndefined(value: string): number | undefined {
  const n = Number(value);
  return Number.isFinite(n) ? n : undefined;
}
