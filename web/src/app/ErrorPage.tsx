import { Link, useRouteError } from 'react-router-dom';
import styles from './ErrorPage.module.css';

export function ErrorPage() {
  const error = useRouteError() as { statusText?: string; message?: string } | null;
  return (
    <div className={styles.wrap} role="alert">
      <h1 className={styles.title}>出了点问题</h1>
      <p className={styles.message}>
        {error?.statusText ?? error?.message ?? '页面加载失败，请稍后再试。'}
      </p>
      <Link to="/" className={styles.link}>
        返回首页
      </Link>
    </div>
  );
}
