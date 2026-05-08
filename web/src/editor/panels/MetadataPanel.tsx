import { useEditorActions, useEditorStore } from '../EditorContext';
import type { Metadata } from '@/types';
import styles from './forms.module.css';

interface FieldDef {
  key: keyof Metadata;
  label: string;
  description?: string;
  required?: boolean;
  placeholder?: string;
  multiline?: boolean;
}

const FIELDS: FieldDef[] = [
  { key: 'title', label: '论文题目', required: true },
  { key: 'subtitle', label: '副标题' },
  { key: 'author', label: '作者姓名', required: true },
  { key: 'studentId', label: '学号', required: true },
  { key: 'college', label: '学院', required: true },
  { key: 'major', label: '专业', required: true },
  { key: 'advisor', label: '指导教师', required: true },
  { key: 'date', label: '日期', required: true, placeholder: '2026-05' },
  { key: 'language', label: '语言', required: true, placeholder: 'zh-CN' }
];

export function MetadataPanel() {
  const metadata = useEditorStore((s) => s.envelope.document.metadata);
  const actions = useEditorActions();

  return (
    <div className={styles.panel}>
      <header className={styles.panelHeader}>
        <h1 className={styles.panelTitle}>论文元数据</h1>
        <p className={styles.panelDesc}>这些字段会被模板用于封面、页眉与目录。</p>
      </header>

      <div className={styles.grid}>
        {FIELDS.map((f) => {
          const value = (metadata[f.key] as string | undefined) ?? '';
          return (
            <label key={f.key} className={styles.field}>
              <span className={styles.label}>
                {f.label}
                {f.required && <span className={styles.required}>*</span>}
              </span>
              {f.multiline ? (
                <textarea
                  className={styles.textarea}
                  value={value}
                  rows={3}
                  placeholder={f.placeholder}
                  onChange={(e) =>
                    actions.updateMetadata({ [f.key]: e.target.value } as Partial<Metadata>)
                  }
                />
              ) : (
                <input
                  className={styles.input}
                  type="text"
                  value={value}
                  placeholder={f.placeholder}
                  onChange={(e) =>
                    actions.updateMetadata({ [f.key]: e.target.value } as Partial<Metadata>)
                  }
                />
              )}
              {f.description && <span className={styles.hint}>{f.description}</span>}
            </label>
          );
        })}
      </div>
    </div>
  );
}
