import { useNavigate } from 'react-router-dom';
import { useMemo, useState } from 'react';
import { Brand } from '@/components/Brand';
import { useCreateDocument, useImportDocument, useTemplates } from '@/api/queries';
import { ThesisApiError } from '@/api/client';
import { deleteLocalDraft, duplicateLocalDraft, listLocalDrafts } from '@/editor/localDrafts';
import { parseThesisDocumentJson } from '@/editor/documentContract';
import styles from './HomePage.module.css';

export function HomePage() {
  const navigate = useNavigate();
  const templatesQuery = useTemplates();
  const createDoc = useCreateDocument();
  const importDoc = useImportDocument();
  const [error, setError] = useState<string | null>(null);
  const [draftTick, setDraftTick] = useState(0);
  const drafts = useMemo(() => listLocalDrafts(), [draftTick]);

  const handleNewBlank = async () => {
    setError(null);
    try {
      const env = await createDoc.mutateAsync({ title: '未命名论文' });
      setDraftTick((v) => v + 1);
      navigate(`/d/${env.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : '创建失败');
    }
  };

  const handleImport = async (file: File) => {
    setError(null);
    try {
      const text = await file.text();
      const parsed = parseThesisDocumentJson(text);
      if (!parsed.ok || !parsed.document) {
        const first = parsed.issues.find((issue) => issue.severity === 'error') ?? parsed.issues[0];
        setError(first ? `${first.message}${first.path ? `（${first.path}）` : ''}` : '导入失败');
        return;
      }
      const warningCount = parsed.issues.filter((issue) => issue.severity === 'warning').length;
      if (warningCount > 0) {
        const proceed = window.confirm(`导入前发现 ${warningCount} 个警告。继续导入并清理为 ThesisDocument JSON？`);
        if (!proceed) return;
      }
      const env = await importDoc.mutateAsync({ document: parsed.document });
      setDraftTick((v) => v + 1);
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
          <a href="/templates/editor">模板编辑器</a>
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

        <section className={styles.templatesPanel}>
          <header className={styles.sectionHead}>
            <h2 className={styles.sectionTitle}>最近本地草稿</h2>
            <span className={styles.more}>浏览器本地保存</span>
          </header>

          {drafts.length === 0 ? (
            <p className={styles.muted}>暂无本地草稿。新建或导入 JSON 后会自动保存。</p>
          ) : (
            <ul className={styles.draftList}>
              {drafts.slice(0, 8).map((draft) => (
                <li key={draft.id} className={styles.draftItem}>
                  <button
                    type="button"
                    className={styles.draftOpen}
                    onClick={() => navigate(`/d/${draft.id}`)}
                  >
                    <strong>{draft.title}</strong>
                    <span>
                      {draft.author || '未填写作者'} · {new Date(draft.updatedAt).toLocaleString()}
                    </span>
                  </button>
                  <button
                    type="button"
                    className={styles.draftAction}
                    onClick={() => {
                      const copy = duplicateLocalDraft(draft.id);
                      setDraftTick((v) => v + 1);
                      navigate(`/d/${copy.id}`);
                    }}
                  >
                    复制
                  </button>
                  <button
                    type="button"
                    className={styles.draftAction}
                    onClick={() => {
                      if (!window.confirm(`删除本地草稿「${draft.title}」？此操作不可撤销。`)) return;
                      deleteLocalDraft(draft.id);
                      setDraftTick((v) => v + 1);
                    }}
                  >
                    删除
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </main>
    </div>
  );
}
