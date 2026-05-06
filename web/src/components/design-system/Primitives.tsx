import { useEffect, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';

export function Button({ variant = 'secondary', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'danger' | 'ghost' }) {
  return <button className={`ui-button ${variant}`} {...props} />;
}

export function IconButton({ children, ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return <button className="ui-icon-button" {...props}>{children}</button>;
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

export function Checkbox({ label, ...props }: InputHTMLAttributes<HTMLInputElement> & { label: string }) {
  return (
    <label className="ui-checkbox">
      <input type="checkbox" {...props} />
      <span>{label}</span>
    </label>
  );
}

export function Field({ label, children, hint, error }: { label: string; children: ReactNode; hint?: string; error?: string }) {
  return (
    <label className={`ui-field ${error ? 'has-error' : ''}`}>
      <span>{label}</span>
      {children}
      {error ? <small role="alert">{error}</small> : hint ? <small>{hint}</small> : null}
    </label>
  );
}

export function Card({ title, description, children, footer }: { title?: string; description?: string; children: ReactNode; footer?: ReactNode }) {
  return (
    <section className="ui-card">
      {title ? (
        <div className="ui-card-heading">
          <h2>{title}</h2>
          {description ? <p>{description}</p> : null}
        </div>
      ) : null}
      {children}
      {footer ? <div className="ui-card-footer">{footer}</div> : null}
    </section>
  );
}

export function Panel({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return (
    <aside className="ui-panel">
      <SectionHeader title={title} description={description} />
      {children}
    </aside>
  );
}

export function SectionHeader({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="ui-section-header">
      <div>
        <h2>{title}</h2>
        {description ? <p>{description}</p> : null}
      </div>
      {action}
    </div>
  );
}

export function Badge({ tone = 'neutral', children }: { tone?: 'neutral' | 'success' | 'warning' | 'danger' | 'info'; children: ReactNode }) {
  return <span className={`ui-badge ${tone}`}>{children}</span>;
}

export function StatusPill({ status, children }: { status: 'ready' | 'draft' | 'notReady' | 'disabled' | 'saved' | 'warning'; children: ReactNode }) {
  const tone = status === 'ready' || status === 'saved' ? 'success' : status === 'notReady' || status === 'disabled' ? 'danger' : 'warning';
  return <Badge tone={tone}>{children}</Badge>;
}

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="ui-empty">
      <strong>{title}</strong>
      {description ? <p>{description}</p> : null}
      {action}
    </div>
  );
}

export function InlineAlert({ tone = 'info', title, children }: { tone?: 'info' | 'success' | 'warning' | 'danger'; title: string; children?: ReactNode }) {
  return (
    <div className={`ui-inline-alert ${tone}`} role={tone === 'danger' ? 'alert' : 'status'}>
      <strong>{title}</strong>
      {children ? <p>{children}</p> : null}
    </div>
  );
}

export function Tabs({ tabs, active, onChange }: { tabs: Array<{ id: string; label: string; badge?: number }>; active: string; onChange: (id: string) => void }) {
  return (
    <div className="ui-tabs" role="tablist" aria-label="侧边面板">
      {tabs.map(tab => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={active === tab.id}
          className={active === tab.id ? 'active' : ''}
          onClick={() => onChange(tab.id)}
        >
          {tab.label}
          {tab.badge ? <span>{tab.badge}</span> : null}
        </button>
      ))}
    </div>
  );
}

export function SegmentedControl({ value, options, onChange, label }: { value: string; options: Array<{ value: string; label: string }>; onChange: (value: string) => void; label: string }) {
  return (
    <div className="ui-segmented" role="radiogroup" aria-label={label}>
      {options.map(option => (
        <button
          key={option.value}
          type="button"
          role="radio"
          aria-checked={value === option.value}
          className={value === option.value ? 'active' : ''}
          onClick={() => onChange(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

export function Tooltip({ children, text }: { children: ReactNode; text: string }) {
  return <span className="ui-tooltip" data-tooltip={text}>{children}</span>;
}

export function Modal({ title, description, children, onClose }: { title: string; description?: string; children: ReactNode; onClose: () => void }) {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  return (
    <div className="ui-modal-backdrop" role="presentation" onMouseDown={event => event.target === event.currentTarget && onClose()}>
      <div className="ui-modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
        <div className="ui-modal-title">
          <div>
            <strong id="modal-title">{title}</strong>
            {description ? <p>{description}</p> : null}
          </div>
          <IconButton aria-label="关闭弹窗" onClick={onClose}>×</IconButton>
        </div>
        {children}
      </div>
    </div>
  );
}
