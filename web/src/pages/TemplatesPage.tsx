import { useNavigate } from 'react-router-dom';
import { Brand } from '@/components/Brand';
import { useCreateDocument, useTemplates } from '@/api/queries';
import styles from './TemplatesPage.module.css';

export function TemplatesPage() {
  const navigate = useNavigate();
  const { data, isLoading, isError } = useTemplates();
  const createDoc = useCreateDocument();

  const start = async (templateId: string) => {
    const env = await createDoc.mutateAsync({ templateId, title: '未命名论文' });
    navigate(`/d/${env.id}`);
  };

  return (
    <div className={styles.page}>
      <header className={styles.topbar}>
        <Brand />
      </header>

      <main className={styles.main}>
        <h1 className={styles.title}>模板库</h1>
        <p className={styles.subtitle}>挑一个学校 / 学院的格式包，一键开始写作。</p>

        {isLoading && <p className={styles.muted}>加载中…</p>}
        {isError && <p className={styles.muted}>无法加载模板</p>}

        <ul className={styles.grid}>
          {(data ?? []).map((t) => (
            <li key={t.id} className={styles.card}>
              <header className={styles.cardHead}>
                <h2 className={styles.name}>{t.name}</h2>
                <span className={styles.version}>v{t.version}</span>
              </header>
              <div className={styles.meta}>
                {t.school} · {t.college}
              </div>
              <div className={styles.tags}>
                {t.tags.map((tag) => (
                  <span key={tag} className={styles.tag}>
                    {tag}
                  </span>
                ))}
              </div>
              <footer className={styles.cardFoot}>
                <span
                  className={styles.status}
                  data-status={t.status === 'ready' ? 'ready' : 'review'}
                >
                  {t.status === 'ready' ? '就绪' : '审阅中'}
                </span>
                <button type="button" onClick={() => start(t.id)} className={styles.useBtn}>
                  用此模板 →
                </button>
              </footer>
            </li>
          ))}
        </ul>
      </main>
    </div>
  );
}
