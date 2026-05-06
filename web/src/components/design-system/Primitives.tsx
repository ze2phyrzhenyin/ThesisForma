import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react';

export function Button({ variant = 'secondary', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'danger' }) {
  return <button className={`ui-button ${variant}`} {...props} />;
}

export function IconButton(props: ButtonHTMLAttributes<HTMLButtonElement>) {
  return <button className="ui-icon-button" {...props} />;
}

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input className="ui-input" {...props} />;
}

export function Textarea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className="ui-textarea" {...props} />;
}

export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className="ui-select" {...props} />;
}

export function Field({ label, children, hint }: { label: string; children: ReactNode; hint?: string }) {
  return (
    <label className="ui-field">
      <span>{label}</span>
      {children}
      {hint ? <small>{hint}</small> : null}
    </label>
  );
}

export function Card({ title, children, footer }: { title?: string; children: ReactNode; footer?: ReactNode }) {
  return (
    <section className="ui-card">
      {title ? <h2>{title}</h2> : null}
      {children}
      {footer ? <div className="ui-card-footer">{footer}</div> : null}
    </section>
  );
}

export function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <aside className="ui-panel">
      <h2>{title}</h2>
      {children}
    </aside>
  );
}

export function Badge({ tone = 'neutral', children }: { tone?: 'neutral' | 'success' | 'warning' | 'danger'; children: ReactNode }) {
  return <span className={`ui-badge ${tone}`}>{children}</span>;
}

export function EmptyState({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <div className="ui-empty">
      <strong>{title}</strong>
      {action}
    </div>
  );
}

export function Modal({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  return (
    <div className="ui-modal-backdrop" role="dialog" aria-modal="true" aria-label={title}>
      <div className="ui-modal">
        <div className="ui-modal-title">
          <strong>{title}</strong>
          <IconButton aria-label="关闭弹窗" onClick={onClose}>×</IconButton>
        </div>
        {children}
      </div>
    </div>
  );
}
