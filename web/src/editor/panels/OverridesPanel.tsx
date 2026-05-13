import { useMemo } from 'react';
import { useFormatPreview } from '@/api/queries';
import { useEditorActions, useEditorStore } from '../EditorContext';
import { SECTION_META } from '../sectionMeta';
import {
  clearOverrides,
  saveOverrides,
  SECTION_BUCKET_FOR_KIND,
  type DocumentOverrides
} from '../overrides';
import styles from './forms.module.css';
import overridesStyles from './OverridesPanel.module.css';
import {
  BUCKETS,
  BucketCard,
  Field,
  FontEditor,
  Group,
  HEADING_LEVELS,
  HeadingCard,
  ParagraphEditor,
  SectionInstanceCard,
  TocSectionPicker,
  TriCheckbox,
  mergeHeading,
  mergeInstance,
  mergeStrip
} from './OverridesPanelControls';
import type { FormatPreviewResponse } from '@/types';

export function OverridesPanel() {
  const envelope = useEditorStore((s) => s.envelope);
  const documentId = envelope.id;
  const sections = envelope.document.sections;
  const overrides = envelope.overrides ?? {};
  const actions = useEditorActions();
  const formatPreview = useFormatPreview();

  const update = (updater: (draft: DocumentOverrides) => void) => {
    let next: DocumentOverrides = {};
    actions.updateOverrides((draft) => {
      updater(draft);
      next = structuredClone(draft);
    });
    saveOverrides(documentId, next);
  };

  const refreshFormatPreview = () => {
    formatPreview.mutate({
      id: documentId,
      templateId: envelope.templateId ?? null,
      overrides
    });
  };

  const tocMin = overrides.toc?.minLevel ?? 1;
  const tocMax = overrides.toc?.maxLevel ?? 3;

  const hasAny = useMemo(() => Object.keys(overrides).length > 0, [overrides]);

  const reset = () => {
    if (!hasAny) return;
    if (!window.confirm('清除当前文档的所有格式覆盖？')) return;
    clearOverrides(documentId);
    actions.updateOverrides((draft) => {
      for (const key of Object.keys(draft) as (keyof DocumentOverrides)[]) {
        delete draft[key];
      }
    });
  };

  return (
    <div className={styles.panel}>
      <header className={styles.panelHeader}>
        <h1 className={styles.panelTitle}>格式覆盖</h1>
        <p className={styles.panelDesc}>
          在模板默认格式之上为本文档做局部调整。
        </p>
      </header>

      <div className={overridesStyles.notice}>
        <strong>结构先行 · 持久化范围</strong>
        <br />
        覆盖随 <code>DocumentEnvelope.overrides</code> 保存，并在校验和渲染时合并到模板格式规则。
        本地旧草稿会从 <code>thesisforma.overrides.v1</code> 迁移。
      </div>

      <Group title="生效证据" desc="查看当前覆盖与模板格式合并后的结果。">
        <FormatPreviewCard
          data={formatPreview.data}
          isPending={formatPreview.isPending}
          error={formatPreview.error}
          onRefresh={refreshFormatPreview}
        />
      </Group>

      {/* ── 目录 ─────────────────────────────────────────────────── */}
      <Group title="目录">
        <div className={styles.grid}>
          <Field label="起始层级" hint="1 = H1">
            <select
              className={styles.input}
              value={tocMin}
              onChange={(e) =>
                update((d) => {
                  d.toc = d.toc ?? {};
                  d.toc.minLevel = Number(e.target.value);
                })
              }
            >
              {[1, 2, 3, 4, 5, 6].map((n) => (
                <option key={n} value={n}>
                  H{n}
                </option>
              ))}
            </select>
          </Field>

          <Field label="结束层级" hint="必须 ≥ 起始层级">
            <select
              className={styles.input}
              value={tocMax}
              onChange={(e) =>
                update((d) => {
                  d.toc = d.toc ?? {};
                  d.toc.maxLevel = Number(e.target.value);
                })
              }
            >
              {[1, 2, 3, 4, 5, 6].map((n) => (
                <option key={n} value={n} disabled={n < tocMin}>
                  H{n}
                </option>
              ))}
            </select>
          </Field>

          <Field label="目录标题文本" hint="留空 = 用模板默认标题" full>
            <input
              className={styles.input}
              type="text"
              placeholder="目录"
              value={overrides.toc?.title ?? ''}
              onChange={(e) =>
                update((d) => {
                  d.toc = d.toc ?? {};
                  d.toc.title = e.target.value;
                })
              }
            />
          </Field>
        </div>

        <h4 className={overridesStyles.subTitle}>引用范围</h4>
        <TocSectionPicker
          sections={sections.map((s, i) => ({
            id: s.id ?? `_section_${i}`,
            kind: s.kind,
            label: s.title ?? SECTION_META[s.kind].label
          }))}
          included={overrides.toc?.includeSectionIds}
          onChange={(ids) =>
            update((d) => {
              d.toc = d.toc ?? {};
              if (ids === undefined) delete d.toc.includeSectionIds;
              else d.toc.includeSectionIds = ids;
            })
          }
        />

        <div className={styles.hint}>
          预览：<code>TOC \o &quot;{tocMin}-{tocMax}&quot; \h \z \u</code>
          {overrides.toc?.includeSectionIds && overrides.toc.includeSectionIds.length > 0 && (
            <>
              {' '}· 仅包含 {overrides.toc.includeSectionIds.length} / {sections.length} 个章节
              （渲染器需按 section bookmark 范围发多个 TOC 字段或合成纯文本目录，详见
              <code> docs/web-overrides-contract.md</code>）
            </>
          )}
        </div>
      </Group>

      {/* ── 默认字体 ─────────────────────────────────────────────── */}
      <Group title="默认字体" desc="覆盖模板的全局默认字体（影响正文与未单独配置的标题）。">
        <FontEditor
          font={overrides.defaultFont ?? {}}
          onChange={(patch) =>
            update((d) => {
              d.defaultFont = mergeStrip(d.defaultFont ?? {}, patch);
            })
          }
        />
      </Group>

      {/* ── 正文段落 ─────────────────────────────────────────────── */}
      <Group title="正文段落" desc="影响 ParagraphBlock 默认呈现的行距、缩进、对齐等。">
        <ParagraphEditor
          paragraph={overrides.bodyParagraph ?? {}}
          onChange={(patch) =>
            update((d) => {
              d.bodyParagraph = mergeStrip(d.bodyParagraph ?? {}, patch);
            })
          }
        />
      </Group>

      {/* ── 各级标题 ─────────────────────────────────────────────── */}
      <Group title="各级标题" desc="按级别覆盖标题样式。常用 H1 / H2 / H3。">
        {HEADING_LEVELS.map((level) => (
          <HeadingCard
            key={level}
            level={level}
            override={overrides.headings?.[level] ?? {}}
            onChange={(patch) =>
              update((d) => {
                d.headings = d.headings ?? {};
                d.headings[level] = mergeHeading(d.headings[level] ?? {}, patch);
              })
            }
          />
        ))}
      </Group>

      {/* ── 全局页眉 / 页脚 ──────────────────────────────────────── */}
      <Group title="页眉 / 页脚（全局）">
        <div className={styles.grid}>
          <Field label="页眉文本" hint="留空 = 用模板默认值" full>
            <input
              className={styles.input}
              type="text"
              value={overrides.headerFooter?.headerText ?? ''}
              onChange={(e) =>
                update((d) => {
                  d.headerFooter = d.headerFooter ?? {};
                  d.headerFooter.headerText = e.target.value;
                })
              }
            />
          </Field>

          <TriCheckbox
            label="页眉下加横线"
            value={overrides.headerFooter?.drawHeaderLine}
            onChange={(v) =>
              update((d) => {
                d.headerFooter = d.headerFooter ?? {};
                if (v === undefined) delete d.headerFooter.drawHeaderLine;
                else d.headerFooter.drawHeaderLine = v;
              })
            }
          />
          <TriCheckbox
            label="封面隐藏页码"
            value={overrides.headerFooter?.hidePageNumberOnCover}
            onChange={(v) =>
              update((d) => {
                d.headerFooter = d.headerFooter ?? {};
                if (v === undefined) delete d.headerFooter.hidePageNumberOnCover;
                else d.headerFooter.hidePageNumberOnCover = v;
              })
            }
          />
          <TriCheckbox
            label="首页与其它页不同"
            value={overrides.headerFooter?.differentFirstPage}
            onChange={(v) =>
              update((d) => {
                d.headerFooter = d.headerFooter ?? {};
                if (v === undefined) delete d.headerFooter.differentFirstPage;
                else d.headerFooter.differentFirstPage = v;
              })
            }
          />
        </div>
      </Group>

      {/* ── 章节分组（3 桶）─────────────────────────────────────── */}
      <Group
        title="章节分组"
        desc="渲染器把 8 种章节折叠成 3 桶（封面 / 前置页 / 正文）；这里是桶级别的覆盖。"
      >
        {BUCKETS.map((bucket) => (
          <BucketCard
            key={bucket}
            bucket={bucket}
            override={overrides.sectionFormats?.[bucket] ?? {}}
            onChange={(patch) =>
              update((d) => {
                d.sectionFormats = d.sectionFormats ?? {};
                d.sectionFormats[bucket] = mergeStrip(d.sectionFormats[bucket] ?? {}, patch);
              })
            }
          />
        ))}
      </Group>

      {/* ── 单节实例覆盖 ─────────────────────────────────────────── */}
      <Group
        title="单节覆盖"
        desc="对某个具体章节做更细的偏离。同字段同时存在时，单节覆盖优先于章节分组。"
      >
        {sections.length === 0 && (
          <div className={overridesStyles.placeholder}>当前文档还没有任何章节。</div>
        )}
        {sections.map((section) => {
          if (!section.id) return null;
          const meta = SECTION_META[section.kind];
          const bucket = SECTION_BUCKET_FOR_KIND[section.kind];
          const inst = overrides.sectionInstances?.[section.id] ?? {};
          return (
            <SectionInstanceCard
              key={section.id}
              sectionId={section.id}
              title={section.title ?? meta.label}
              bucket={bucket}
              instance={inst}
              onChange={(patch) =>
                update((d) => {
                  d.sectionInstances = d.sectionInstances ?? {};
                  const merged = mergeInstance(d.sectionInstances[section.id!] ?? {}, patch);
                  if (Object.keys(merged).length === 0) {
                    delete d.sectionInstances[section.id!];
                  } else {
                    d.sectionInstances[section.id!] = merged;
                  }
                })
              }
            />
          );
        })}
      </Group>

      <div className={overridesStyles.footer}>
        <button
          type="button"
          className={overridesStyles.resetBtn}
          onClick={reset}
          disabled={!hasAny}
        >
          清除全部覆盖
        </button>
      </div>
    </div>
  );
}

interface FormatPreviewCardProps {
  data: FormatPreviewResponse | undefined;
  isPending: boolean;
  error: Error | null;
  onRefresh(): void;
}

function FormatPreviewCard({ data, isPending, error, onRefresh }: FormatPreviewCardProps) {
  const changes = data?.changes ?? [];
  const sectionChanges = data?.sections.filter((section) => section.changes.length > 0) ?? [];

  return (
    <div className={overridesStyles.evidenceCard}>
      <div className={overridesStyles.evidenceHeader}>
        <div>
          <div className={overridesStyles.evidenceTitle}>
            {data ? data.templateName || data.templateId || 'TemplatePackage' : 'Resolved ThesisFormatSpec'}
          </div>
          <div className={overridesStyles.evidenceMeta}>
            {data ? `${data.formatSpecName || 'format spec'} · ${changes.length} 项变更` : '尚未刷新'}
          </div>
        </div>
        <button
          type="button"
          className={overridesStyles.evidenceBtn}
          onClick={onRefresh}
          disabled={isPending}
        >
          {isPending ? '刷新中…' : '刷新生效证据'}
        </button>
      </div>

      {error && <div className={overridesStyles.evidenceError}>{error.message}</div>}

      {data && (
        <>
          {changes.length === 0 ? (
            <div className={overridesStyles.evidenceEmpty}>
              {data.evidence[0]?.message ?? '当前覆盖没有改变模板格式。'}
            </div>
          ) : (
            <ul className={overridesStyles.changeList}>
              {changes.slice(0, 10).map((change) => (
                <li key={`${change.path}-${change.source}`} className={overridesStyles.changeItem}>
                  <span>{change.label}</span>
                  <code>{change.before ?? '<none>'}</code>
                  <span className={overridesStyles.arrow}>→</span>
                  <code>{change.after ?? '<none>'}</code>
                </li>
              ))}
            </ul>
          )}

          {changes.length > 10 && (
            <div className={overridesStyles.evidenceMeta}>另有 {changes.length - 10} 项变更已包含在 API 响应中。</div>
          )}

          {sectionChanges.length > 0 && (
            <div className={overridesStyles.sectionEvidence}>
              {sectionChanges.map((section) => (
                <span key={section.sectionId || `${section.sectionKind}-${section.bucket}`} className={overridesStyles.sectionChip}>
                  {section.title || section.sectionKind}: {section.changes.length}
                </span>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
