import { useEditorActions, useEditorStore } from '../EditorContext';
import { useTemplate } from '@/api/queries';
import type { Metadata, TemplateVariable } from '@/types';
import drawerStyles from './drawer.module.css';
import styles from './forms.module.css';

const TYPE_LABELS: Record<TemplateVariable['type'], string> = {
  string: '文本',
  multilineText: '多行文本',
  date: '日期',
  number: '数字',
  boolean: '开关',
  enum: '枚举',
  image: '图片',
  richText: '富文本'
};

export function VariablesPanel() {
  const templateId = useEditorStore((s) => s.envelope.templateId);
  const metadata = useEditorStore((s) => s.envelope.document.metadata);
  const actions = useEditorActions();
  const { data, isLoading, isError } = useTemplate(templateId ?? undefined);

  if (!templateId) {
    return (
      <div className={styles.panel}>
        <header className={styles.panelHeader}>
          <h1 className={styles.panelTitle}>模板变量</h1>
          <p className={styles.panelDesc}>当前未选择模板。</p>
        </header>
        <div className={styles.placeholder}>
          可在「模板库」选择一份模板，进入编辑器后这里会显示其变量列表。
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className={styles.panel}>
        <header className={styles.panelHeader}>
          <h1 className={styles.panelTitle}>模板变量</h1>
        </header>
        <div className={styles.placeholder}>加载模板中…</div>
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className={styles.panel}>
        <header className={styles.panelHeader}>
          <h1 className={styles.panelTitle}>模板变量</h1>
        </header>
        <div className={drawerStyles.empty}>无法加载模板。</div>
      </div>
    );
  }

  const variables = [...data.variables].sort(
    (a, b) => (a.displayOrder ?? 100) - (b.displayOrder ?? 100)
  );

  return (
    <div className={styles.panel}>
      <header className={styles.panelHeader}>
        <h1 className={styles.panelTitle}>模板变量</h1>
        <p className={styles.panelDesc}>
          模板「{data.summary.name}」需要以下变量。绑定到元数据的变量会跟随元数据更新。
        </p>
      </header>

      {variables.length === 0 ? (
        <div className={drawerStyles.empty}>该模板未声明任何变量。</div>
      ) : (
        <div className={styles.grid}>
          {variables.map((v) => {
            const linkedMetaKey = mapSourcePathToMetadataKey(v.sourcePath);
            const linkedValue = linkedMetaKey ? (metadata[linkedMetaKey] as string) : undefined;

            return (
              <div key={v.name} className={styles.field}>
                <span className={styles.label}>
                  {v.label ?? v.name}
                  {v.required && <span className={styles.required}>*</span>}
                  <span className={varTypeChipStyle()}>{TYPE_LABELS[v.type]}</span>
                </span>
                {linkedMetaKey ? (
                  <input
                    className={styles.input}
                    value={linkedValue ?? ''}
                    placeholder={String(v.defaultValue ?? '')}
                    onChange={(e) =>
                      actions.updateMetadata({ [linkedMetaKey]: e.target.value } as Partial<Metadata>)
                    }
                  />
                ) : (
                  <input
                    className={styles.input}
                    value={String(v.defaultValue ?? '')}
                    readOnly
                    title="此变量当前由模板默认值决定，覆盖能力计划在后续阶段加入。"
                  />
                )}
                <span className={styles.hint}>
                  {linkedMetaKey
                    ? `已绑定元数据「${linkedMetaKey}」`
                    : v.description ?? '使用模板默认值（暂不可在前端覆盖）'}
                </span>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

function mapSourcePathToMetadataKey(sourcePath?: string): keyof Metadata | null {
  if (!sourcePath) return null;
  // Common patterns: "metadata.title", "$.metadata.author", "thesis.metadata.college"
  const match = sourcePath.match(/metadata\.(\w+)/);
  if (!match) return null;
  const key = match[1] as keyof Metadata;
  const known: ReadonlyArray<keyof Metadata> = [
    'title',
    'subtitle',
    'author',
    'college',
    'major',
    'studentId',
    'advisor',
    'date',
    'language'
  ];
  return known.includes(key) ? key : null;
}

function varTypeChipStyle(): string {
  return [styles.label, 'tf-var-type-chip'].join(' ');
}
