import { useNavigate } from 'react-router-dom';
import { useState } from 'react';
import { Brand } from '@/components/Brand';
import { useCreateDocument, useImportDocument, useTemplates } from '@/api/queries';
import { ThesisApiError } from '@/api/client';
import type { ThesisDocument } from '@/types';
import styles from './HomePage.module.css';

export function HomePage() {
  const navigate = useNavigate();
  const templatesQuery = useTemplates();
  const createDoc = useCreateDocument();
  const importDoc = useImportDocument();
  const [error, setError] = useState<string | null>(null);

  const handleNewBlank = async () => {
    setError(null);
    try {
      const env = await createDoc.mutateAsync({ title: '未命名论文' });
      navigate(`/d/${env.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : '创建失败');
    }
  };

  const handleImport = async (file: File) => {
    setError(null);
    try {
      const text = await file.text();
      const document = JSON.parse(text) as ThesisDocument;
      const env = await importDoc.mutateAsync({ document });
      navigate(`/d/${env.id}`);
    } catch (e) {
      if (e instanceof ThesisApiError && e.payload?.issues?.length) {
        setError(`${e.payload.message}（${e.payload.issues.length} 个问题）`);
      } else {
        setError(e instanceof Error ? e.message : '导入失败');
      }
    }
  };

  const onDrop = (ev: React.DragEvent) => {
    ev.preventDefault();
    const file = ev.dataTransfer.files[0];
    if (file?.name.endsWith('.json')) handleImport(file);
    else setError('请拖入 .json 文件');
  };

  const templates = templatesQuery.data ?? [];

  return (
    <div className={styles.page}>
      <header className={styles.topbar}>
        <Brand />
        <nav className={styles.nav}>
          <a href="/templates">模板库</a>
        </nav>
      </header>

      <main className={styles.main}>
        <section className={styles.hero}>
          <h1 className={styles.title}>把论文写进结构里</h1>
          <p className={styles.subtitle}>
            按章节、标题、段落、图、表、公式输入，剩下的格式交给学校模板。
          </p>

          <div className={styles.actions}>
            <button
              type="button"
              className={styles.primary}
              onClick={handleNewBlank}
              disabled={createDoc.isPending}
            >
              {createDoc.isPending ? '创建中…' : '新建论文'}
            </button>

            <label
              className={styles.dropzone}
              onDragOver={(e) => e.preventDefault()}
              onDrop={onDrop}
            >
              <input
                type="file"
                accept="application/json,.json"
                hidden
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) handleImport(f);
                }}
              />
              <span className={styles.dropzoneIcon}>↑</span>
              <span>
                <strong>导入 JSON</strong>
                <span className={styles.dropzoneHint}>拖入 thesis-document.json</span>
              </span>
            </label>
          </div>

          {error && (
            <div className={styles.error} role="alert">
              {error}
            </div>
          )}
        </section>

        <section className={styles.templatesPanel}>
          <header className={styles.sectionHead}>
            <h2 className={styles.sectionTitle}>选个学校模板开始</h2>
            <a href="/templates" className={styles.more}>
              全部模板 →
            </a>
          </header>

          {templatesQuery.isLoading && <p className={styles.muted}>加载中…</p>}
          {templatesQuery.isError && (
            <p className={styles.muted}>无法加载模板列表（API 是否在运行？）</p>
          )}

          <ul className={styles.templateGrid}>
            {templates.slice(0, 6).map((t) => (
              <li key={t.id}>
                <button
                  type="button"
                  className={styles.templateCard}
                  onClick={async () => {
                    const env = await createDoc.mutateAsync({
                      templateId: t.id,
                      title: '未命名论文'
                    });
                    navigate(`/d/${env.id}`);
                  }}
                >
                  <div className={styles.templateName}>{t.name}</div>
                  <div className={styles.templateMeta}>
                    {t.school} · {t.college}
                  </div>
                  <div className={styles.templateTags}>
                    {t.tags.slice(0, 3).map((tag) => (
                      <span key={tag} className={styles.tag}>
                        {tag}
                      </span>
                    ))}
                  </div>
                </button>
              </li>
            ))}
          </ul>
        </section>
      </main>
    </div>
  );
}
