import { Link } from 'react-router-dom';
import styles from './Brand.module.css';

interface Props {
  size?: 'sm' | 'md';
}

export function Brand({ size = 'md' }: Props) {
  return (
    <Link to="/" className={styles.brand} data-size={size} aria-label="ThesisForma 首页">
      <span className={styles.mark} aria-hidden>
        <svg viewBox="0 0 32 32" width="100%" height="100%">
          <rect width="32" height="32" rx="6" fill="currentColor" />
          <path d="M9 9h14v3.5H9zM9 15h14v2H9zM9 19.5h10v2H9z" fill="#fff" />
        </svg>
      </span>
      <span className={styles.text}>ThesisForma</span>
    </Link>
  );
}
