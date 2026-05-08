import { useEffect, useMemo, useState } from 'react';
import { useEditorStore } from '../EditorContext';
import { SECTION_META } from '../sectionMeta';
import {
  clearOverrides,
  loadOverrides,
  saveOverrides,
  SECTION_BUCKET_FOR_KIND,
  SECTION_BUCKET_LABEL,
  type DocumentOverrides,
  type FontOverride,
  type HeadingLevel,
  type HeadingOverride,
  type PageNumberStyle,
  type ParagraphOverride,
  type SectionBucket,
  type SectionFormatOverride,
  type SectionInstanceOverride
} from '../overrides';
import type { TextAlignment } from '@/types';
import styles from './forms.module.css';
import overridesStyles from './OverridesPanel.module.css';

const PAGE_NUMBER_STYLES: { value: PageNumberStyle; label: string }[] = [
  { value: 'none', label: '无' },
  { value: 'decimal', label: '1, 2, 3' },
  { value: 'lowerRoman', label: 'i, ii, iii' },
  { value: 'upperRoman', label: 'I, II, III' }
];

const ALIGNMENTS: { value: TextAlignment; label: string }[] = [
  { value: 'left', label: '左' },
  { value: 'center', label: '中' },
  { value: 'right', label: '右' },
  { value: 'both', label: '两端' }
];

const BUCKETS: SectionBucket[] = ['cover', 'frontMatter', 'body'];
const HEADING_LEVELS: HeadingLevel[] = ['1', '2', '3', '4', '5', '6'];

export function OverridesPanel() {
  const documentId = useEditorStore((s) => s.envelope.id);
  const sections = useEditorStore((s) => s.envelope.document.sections);
  const [overrides, setOverrides] = useState<DocumentOverrides>(() => loadOverrides(documentId));

  useEffect(() => {
    setOverrides(loadOverrides(documentId));
  }, [documentId]);

  const update = (updater: (draft: DocumentOverrides) => void) => {
    setOverrides((current) => {
      const next = structuredClone(current);
      updater(next);
      saveOverrides(documentId, next);
      return next;
    });
  };

  const tocMin = overrides.toc?.minLevel ?? 1;
  const tocMax = overrides.toc?.maxLevel ?? 3;

  const hasAny = useMemo(() => Object.keys(overrides).length > 0, [overrides]);

  const reset = () => {
    if (!hasAny) return;
    if (!window.confirm('清除当前文档的所有格式覆盖？')) return;
    clearOverrides(documentId);
    setOverrides({});
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
        覆盖目前保存在浏览器本地（<code>thesisforma.overrides.v1</code>）。后端 stage 4
        接通 <code>DocumentEnvelope.overrides</code> 后会随保存/渲染请求一同上传。
        契约见 <code>docs/web-overrides-contract.md</code>。
      </div>

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

// ───── Reusable layout ──────────────────────────────────────────

interface GroupProps {
  title: string;
  desc?: string;
  children: React.ReactNode;
}

function Group({ title, desc, children }: GroupProps) {
  return (
    <section className={overridesStyles.section}>
      <h2 className={overridesStyles.sectionTitle}>{title}</h2>
      {desc && <p className={overridesStyles.sectionDesc}>{desc}</p>}
      {children}
    </section>
  );
}

interface FieldProps {
  label: string;
  hint?: string;
  full?: boolean;
  children: React.ReactNode;
}

function Field({ label, hint, full, children }: FieldProps) {
  return (
    <div className={`${styles.field} ${full ? overridesStyles.fullRow : ''}`}>
      <label className={styles.label}>
        {label}
        {hint && <span className={styles.hint}> · {hint}</span>}
      </label>
      {children}
    </div>
  );
}

// ───── Font editor ──────────────────────────────────────────────

interface FontEditorProps {
  font: FontOverride;
  onChange(patch: Partial<FontOverride>): void;
}

function FontEditor({ font, onChange }: FontEditorProps) {
  return (
    <div className={styles.grid}>
      <Field label="中文字体" hint="如 宋体 / 仿宋_GB2312">
        <input
          className={styles.input}
          type="text"
          placeholder="（不覆盖）"
          value={font.eastAsia ?? ''}
          onChange={(e) => onChange({ eastAsia: e.target.value === '' ? undefined : e.target.value })}
        />
      </Field>
      <Field label="西文字体" hint="如 Times New Roman">
        <input
          className={styles.input}
          type="text"
          placeholder="（不覆盖）"
          value={font.latin ?? ''}
          onChange={(e) => onChange({ latin: e.target.value === '' ? undefined : e.target.value })}
        />
      </Field>
      <Field label="字号 (pt)" hint="1–72">
        <input
          className={styles.input}
          type="number"
          min={1}
          max={72}
          step={0.5}
          placeholder="（不覆盖）"
          value={font.sizePt ?? ''}
          onChange={(e) => onChange({ sizePt: e.target.value === '' ? undefined : Number(e.target.value) })}
        />
      </Field>
      <div className={overridesStyles.checkboxRow}>
        <TriCheckbox
          label="加粗"
          value={font.bold}
          onChange={(v) => onChange({ bold: v })}
        />
        <TriCheckbox
          label="斜体"
          value={font.italic}
          onChange={(v) => onChange({ italic: v })}
        />
      </div>
    </div>
  );
}

// ───── Paragraph editor ─────────────────────────────────────────

interface ParagraphEditorProps {
  paragraph: ParagraphOverride;
  onChange(patch: Partial<ParagraphOverride>): void;
}

function ParagraphEditor({ paragraph, onChange }: ParagraphEditorProps) {
  return (
    <div className={styles.grid}>
      <Field label="行距倍数" hint="0.5–4，常见 1.5 / 1.75 / 2">
        <input
          className={styles.input}
          type="number"
          min={0.5}
          max={4}
          step={0.05}
          placeholder="（不覆盖）"
          value={paragraph.lineSpacingMultiple ?? ''}
          onChange={(e) =>
            onChange({ lineSpacingMultiple: e.target.value === '' ? undefined : Number(e.target.value) })
          }
        />
      </Field>
      <Field label="对齐">
        <select
          className={styles.input}
          value={paragraph.alignment ?? ''}
          onChange={(e) =>
            onChange({ alignment: e.target.value === '' ? undefined : (e.target.value as TextAlignment) })
          }
        >
          <option value="">（不覆盖）</option>
          {ALIGNMENTS.map((a) => (
            <option key={a.value} value={a.value}>
              {a.label}
            </option>
          ))}
        </select>
      </Field>
      <Field label="段前 (pt)" hint="0–72">
        <input
          className={styles.input}
          type="number"
          min={0}
          max={72}
          step={0.5}
          placeholder="（不覆盖）"
          value={paragraph.spaceBeforePt ?? ''}
          onChange={(e) =>
            onChange({ spaceBeforePt: e.target.value === '' ? undefined : Number(e.target.value) })
          }
        />
      </Field>
      <Field label="段后 (pt)" hint="0–72">
        <input
          className={styles.input}
          type="number"
          min={0}
          max={72}
          step={0.5}
          placeholder="（不覆盖）"
          value={paragraph.spaceAfterPt ?? ''}
          onChange={(e) =>
            onChange({ spaceAfterPt: e.target.value === '' ? undefined : Number(e.target.value) })
          }
        />
      </Field>
      <Field label="首行缩进（字符）" hint="0–8，正文常用 2">
        <input
          className={styles.input}
          type="number"
          min={0}
          max={8}
          step={0.5}
          placeholder="（不覆盖）"
          value={paragraph.firstLineIndentChars ?? ''}
          onChange={(e) =>
            onChange({ firstLineIndentChars: e.target.value === '' ? undefined : Number(e.target.value) })
          }
        />
      </Field>
      <Field label="悬挂缩进 (cm)" hint="0–5">
        <input
          className={styles.input}
          type="number"
          min={0}
          max={5}
          step={0.1}
          placeholder="（不覆盖）"
          value={paragraph.hangingIndentCm ?? ''}
          onChange={(e) =>
            onChange({ hangingIndentCm: e.target.value === '' ? undefined : Number(e.target.value) })
          }
        />
      </Field>
      <TriCheckbox
        label="孤行控制（widow control）"
        value={paragraph.widowControl}
        onChange={(v) => onChange({ widowControl: v })}
      />
    </div>
  );
}

// ───── Heading card ─────────────────────────────────────────────

interface HeadingCardProps {
  level: HeadingLevel;
  override: HeadingOverride;
  onChange(patch: Partial<HeadingOverride>): void;
}

function HeadingCard({ level, override, onChange }: HeadingCardProps) {
  const [open, setOpen] = useState(false);
  const hasOverride = Object.keys(override).length > 0;

  return (
    <details
      className={overridesStyles.bucket}
      open={open || hasOverride}
      onToggle={(e) => setOpen((e.target as HTMLDetailsElement).open)}
    >
      <summary className={overridesStyles.bucketTitle}>
        H{level} 标题 {hasOverride && <span className={overridesStyles.tag}>已覆盖</span>}
      </summary>

      <div className={overridesStyles.bucketBody}>
        <FontEditor
          font={override.font ?? {}}
          onChange={(patch) =>
            onChange({
              font: mergeStrip(override.font ?? {}, patch)
            })
          }
        />
        <div className={styles.grid}>
          <Field label="段前 (pt)">
            <input
              className={styles.input}
              type="number"
              min={0}
              max={72}
              step={0.5}
              placeholder="（不覆盖）"
              value={override.spaceBeforePt ?? ''}
              onChange={(e) =>
                onChange({ spaceBeforePt: e.target.value === '' ? undefined : Number(e.target.value) })
              }
            />
          </Field>
          <Field label="段后 (pt)">
            <input
              className={styles.input}
              type="number"
              min={0}
              max={72}
              step={0.5}
              placeholder="（不覆盖）"
              value={override.spaceAfterPt ?? ''}
              onChange={(e) =>
                onChange({ spaceAfterPt: e.target.value === '' ? undefined : Number(e.target.value) })
              }
            />
          </Field>
          <Field label="对齐">
            <select
              className={styles.input}
              value={override.alignment ?? ''}
              onChange={(e) =>
                onChange({
                  alignment: e.target.value === '' ? undefined : (e.target.value as TextAlignment)
                })
              }
            >
              <option value="">（不覆盖）</option>
              {ALIGNMENTS.map((a) => (
                <option key={a.value} value={a.value}>
                  {a.label}
                </option>
              ))}
            </select>
          </Field>
          <Field label="大纲层级" hint="0–8（影响导航视图）">
            <input
              className={styles.input}
              type="number"
              min={0}
              max={8}
              step={1}
              placeholder="（不覆盖）"
              value={override.outlineLevel ?? ''}
              onChange={(e) =>
                onChange({ outlineLevel: e.target.value === '' ? undefined : Number(e.target.value) })
              }
            />
          </Field>
          <TriCheckbox
            label="编号"
            value={override.numbered}
            onChange={(v) => onChange({ numbered: v })}
          />
          <TriCheckbox
            label="另起一页"
            value={override.pageBreakBefore}
            onChange={(v) => onChange({ pageBreakBefore: v })}
          />
        </div>
      </div>
    </details>
  );
}

// ───── Bucket card ──────────────────────────────────────────────

interface BucketCardProps {
  bucket: SectionBucket;
  override: SectionFormatOverride;
  onChange(patch: Partial<SectionFormatOverride>): void;
}

function BucketCard({ bucket, override, onChange }: BucketCardProps) {
  return (
    <div className={overridesStyles.bucket}>
      <div className={overridesStyles.bucketTitle}>{SECTION_BUCKET_LABEL[bucket]}</div>
      <div className={styles.grid}>
        <Field label="页码样式">
          <select
            className={styles.input}
            value={override.pageNumberStyle ?? ''}
            onChange={(e) =>
              onChange({
                pageNumberStyle: e.target.value === '' ? undefined : (e.target.value as PageNumberStyle)
              })
            }
          >
            <option value="">（不覆盖）</option>
            {PAGE_NUMBER_STYLES.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
        </Field>
        <Field label="起始页码">
          <input
            className={styles.input}
            type="number"
            min={1}
            max={999}
            placeholder="（不覆盖）"
            value={override.startPageNumber ?? ''}
            onChange={(e) =>
              onChange({ startPageNumber: e.target.value === '' ? undefined : Number(e.target.value) })
            }
          />
        </Field>
        <TriCheckbox
          label="重置页码"
          value={override.restartPageNumbering}
          onChange={(v) => onChange({ restartPageNumbering: v })}
        />
        <TriCheckbox
          label="显示页眉"
          value={override.includeHeader}
          onChange={(v) => onChange({ includeHeader: v })}
        />
        <TriCheckbox
          label="显示页脚"
          value={override.includeFooter}
          onChange={(v) => onChange({ includeFooter: v })}
        />
      </div>
    </div>
  );
}

// ───── Section instance card ────────────────────────────────────

interface SectionInstanceCardProps {
  sectionId: string;
  title: string;
  bucket: SectionBucket;
  instance: SectionInstanceOverride;
  onChange(patch: Partial<SectionInstanceOverride>): void;
}

function SectionInstanceCard({ sectionId, title, bucket, instance, onChange }: SectionInstanceCardProps) {
  const [open, setOpen] = useState(false);
  const hasOverride = Object.keys(instance).length > 0;

  return (
    <details
      className={overridesStyles.bucket}
      open={open || hasOverride}
      onToggle={(e) => setOpen((e.target as HTMLDetailsElement).open)}
    >
      <summary className={overridesStyles.bucketTitle}>
        {title} <span className={overridesStyles.tag}>{SECTION_BUCKET_LABEL[bucket]}</span>
        {hasOverride && <span className={overridesStyles.tag}>已覆盖</span>}
        <span className={overridesStyles.idTag} title={`section.id = ${sectionId}`}>
          #{sectionId}
        </span>
      </summary>

      <div className={overridesStyles.bucketBody}>
        <h4 className={overridesStyles.subTitle}>页眉 / 页脚 / 页码</h4>
        <div className={styles.grid}>
          <Field label="本节页眉文本" hint="留空 = 跟随全局" full>
            <input
              className={styles.input}
              type="text"
              value={instance.headerText ?? ''}
              onChange={(e) =>
                onChange({ headerText: e.target.value === '' ? undefined : e.target.value })
              }
            />
          </Field>
          <Field label="本节页脚文本" hint="留空 = 跟随全局" full>
            <input
              className={styles.input}
              type="text"
              value={instance.footerText ?? ''}
              onChange={(e) =>
                onChange({ footerText: e.target.value === '' ? undefined : e.target.value })
              }
            />
          </Field>
          <Field label="页码样式">
            <select
              className={styles.input}
              value={instance.pageNumberStyle ?? ''}
              onChange={(e) =>
                onChange({
                  pageNumberStyle:
                    e.target.value === '' ? undefined : (e.target.value as PageNumberStyle)
                })
              }
            >
              <option value="">（不覆盖）</option>
              {PAGE_NUMBER_STYLES.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </Field>
          <Field label="起始页码">
            <input
              className={styles.input}
              type="number"
              min={1}
              max={999}
              placeholder="（不覆盖）"
              value={instance.startPageNumber ?? ''}
              onChange={(e) =>
                onChange({ startPageNumber: e.target.value === '' ? undefined : Number(e.target.value) })
              }
            />
          </Field>
          <TriCheckbox
            label="重置页码"
            value={instance.restartPageNumbering}
            onChange={(v) => onChange({ restartPageNumbering: v })}
          />
          <TriCheckbox
            label="显示页眉"
            value={instance.includeHeader}
            onChange={(v) => onChange({ includeHeader: v })}
          />
          <TriCheckbox
            label="显示页脚"
            value={instance.includeFooter}
            onChange={(v) => onChange({ includeFooter: v })}
          />
        </div>

        <h4 className={overridesStyles.subTitle}>本节默认字体</h4>
        <FontEditor
          font={instance.defaultFont ?? {}}
          onChange={(patch) =>
            onChange({ defaultFont: mergeStrip(instance.defaultFont ?? {}, patch) })
          }
        />

        <h4 className={overridesStyles.subTitle}>本节段落格式</h4>
        <ParagraphEditor
          paragraph={instance.paragraph ?? {}}
          onChange={(patch) =>
            onChange({ paragraph: mergeStrip(instance.paragraph ?? {}, patch) })
          }
        />
      </div>
    </details>
  );
}

// ───── TOC section picker ───────────────────────────────────────

interface TocSectionPickerProps {
  sections: { id: string; kind: string; label: string }[];
  included: string[] | undefined;
  onChange(ids: string[] | undefined): void;
}

function TocSectionPicker({ sections, included, onChange }: TocSectionPickerProps) {
  const allMode = included === undefined;
  const isIncluded = (id: string) => allMode || included!.includes(id);

  const toggleAll = () => {
    if (allMode) {
      // collapse to explicit "all" so user can start unchecking
      onChange(sections.map((s) => s.id));
    } else {
      onChange(undefined);
    }
  };

  const toggleOne = (id: string) => {
    if (allMode) {
      onChange(sections.filter((s) => s.id !== id).map((s) => s.id));
      return;
    }
    const list = included!;
    if (list.includes(id)) {
      const next = list.filter((x) => x !== id);
      onChange(next.length === sections.length ? undefined : next);
    } else {
      const next = [...list, id];
      onChange(next.length === sections.length ? undefined : next);
    }
  };

  return (
    <div className={overridesStyles.tocPicker}>
      <button
        type="button"
        className={overridesStyles.tocPickerAll}
        onClick={toggleAll}
        data-active={allMode}
      >
        <span className={overridesStyles.tocPickerCheck}>{allMode ? '✓' : '–'}</span>
        全部章节
      </button>
      <div className={overridesStyles.tocPickerList}>
        {sections.length === 0 && (
          <div className={overridesStyles.placeholder}>当前文档还没有任何章节。</div>
        )}
        {sections.map((s) => {
          const checked = isIncluded(s.id);
          return (
            <button
              key={s.id}
              type="button"
              className={overridesStyles.tocPickerItem}
              data-active={checked}
              onClick={() => toggleOne(s.id)}
            >
              <span className={overridesStyles.tocPickerCheck}>{checked ? '✓' : ''}</span>
              <span className={overridesStyles.tocPickerLabel}>{s.label}</span>
              <span className={overridesStyles.idTag}>#{s.id}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

// ───── Tri-state checkbox ───────────────────────────────────────

interface TriCheckboxProps {
  label: string;
  value: boolean | undefined;
  onChange(v: boolean | undefined): void;
}

/** undefined = 不覆盖；true = 是；false = 否。点击循环。 */
function TriCheckbox({ label, value, onChange }: TriCheckboxProps) {
  const state = value === undefined ? 'inherit' : value ? 'on' : 'off';
  const cycle = () => {
    if (state === 'inherit') onChange(true);
    else if (state === 'on') onChange(false);
    else onChange(undefined);
  };
  const stateLabel = state === 'inherit' ? '不覆盖' : state === 'on' ? '是' : '否';
  return (
    <button
      type="button"
      className={overridesStyles.triCheckbox}
      onClick={cycle}
      data-state={state}
      title="点击切换：不覆盖 → 是 → 否 → 不覆盖"
    >
      <span className={overridesStyles.triCheckboxBox}>
        {state === 'on' ? '✓' : state === 'off' ? '✕' : '–'}
      </span>
      <span className={overridesStyles.triCheckboxLabel}>{label}</span>
      <span className={overridesStyles.triCheckboxState}>{stateLabel}</span>
    </button>
  );
}

// ───── Helpers ──────────────────────────────────────────────────

/** Apply a partial patch and remove any keys that became undefined or empty objects. */
function mergeStrip<T>(current: T, patch: Partial<T>): T {
  const merged = { ...(current as object), ...(patch as object) } as Record<string, unknown>;
  for (const key of Object.keys(merged)) {
    const v = merged[key];
    if (v === undefined) {
      delete merged[key];
      continue;
    }
    if (typeof v === 'object' && v !== null && !Array.isArray(v) && Object.keys(v).length === 0) {
      delete merged[key];
    }
  }
  return merged as T;
}

function mergeHeading(current: HeadingOverride, patch: Partial<HeadingOverride>): HeadingOverride {
  return mergeStrip(current, patch);
}

function mergeInstance(
  current: SectionInstanceOverride,
  patch: Partial<SectionInstanceOverride>
): SectionInstanceOverride {
  return mergeStrip(current, patch);
}
