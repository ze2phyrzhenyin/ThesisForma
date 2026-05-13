import { useState } from 'react';
import {
  SECTION_BUCKET_LABEL,
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

export const BUCKETS: SectionBucket[] = ['cover', 'frontMatter', 'body'];
export const HEADING_LEVELS: HeadingLevel[] = ['1', '2', '3', '4', '5', '6'];

// ───── Reusable layout ──────────────────────────────────────────

interface GroupProps {
  title: string;
  desc?: string;
  children: React.ReactNode;
}

export function Group({ title, desc, children }: GroupProps) {
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

export function Field({ label, hint, full, children }: FieldProps) {
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

export function FontEditor({ font, onChange }: FontEditorProps) {
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

export function ParagraphEditor({ paragraph, onChange }: ParagraphEditorProps) {
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
      <Field label="固定行距 (pt)" hint="留空则使用倍数">
        <input
          className={styles.input}
          type="number"
          min={1}
          max={120}
          step={0.5}
          placeholder="（不覆盖）"
          value={paragraph.lineSpacingExactPt ?? ''}
          onChange={(e) =>
            onChange({ lineSpacingExactPt: e.target.value === '' ? undefined : Number(e.target.value) })
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

export function HeadingCard({ level, override, onChange }: HeadingCardProps) {
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

export function BucketCard({ bucket, override, onChange }: BucketCardProps) {
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

export function SectionInstanceCard({ sectionId, title, bucket, instance, onChange }: SectionInstanceCardProps) {
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

export function TocSectionPicker({ sections, included, onChange }: TocSectionPickerProps) {
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
export function TriCheckbox({ label, value, onChange }: TriCheckboxProps) {
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
export function mergeStrip<T>(current: T, patch: Partial<T>): T {
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

export function mergeHeading(current: HeadingOverride, patch: Partial<HeadingOverride>): HeadingOverride {
  return mergeStrip(current, patch);
}

export function mergeInstance(
  current: SectionInstanceOverride,
  patch: Partial<SectionInstanceOverride>
): SectionInstanceOverride {
  return mergeStrip(current, patch);
}
