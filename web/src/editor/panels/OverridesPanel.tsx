import styles from './forms.module.css';

export function OverridesPanel() {
  return (
    <div className={styles.panel}>
      <header className={styles.panelHeader}>
        <h1 className={styles.panelTitle}>格式覆盖</h1>
        <p className={styles.panelDesc}>在模板的格式之上做局部调整（页边距、字号、行距等）。</p>
      </header>
      <div className={styles.placeholder}>阶段 4 实现：8–10 项常用格式覆盖。</div>
    </div>
  );
}
