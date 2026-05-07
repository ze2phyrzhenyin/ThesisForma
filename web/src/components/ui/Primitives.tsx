import {
  useEffect,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes
} from 'react';

type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info';

/* -------------------------------------------------------------------------- */
/* Buttons                                                                     */
/* -------------------------------------------------------------------------- */

export function Button({
  variant = 'secondary',
  size,
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  size?: 'sm' | 'lg';
}) {
  const classes = ['btn', variant === 'secondary' ? '' : variant, size ?? '', className ?? '']
    .filter(Boolean)
    .join(' ');
  return <button className={classes} {...props} />;
}

export function IconButton({
  className,
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button className={['icon-btn', className ?? ''].filter(Boolean).join(' ')} {...props}>
      {children}
    </button>
  );
}

/* -------------------------------------------------------------------------- */
/* Form controls                                                               */
/* -------------------------------------------------------------------------- */

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={['input', props.className ?? ''].filter(Boolean).join(' ')} />;
}

export function Textarea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      {...props}
      className={['textarea', props.className ?? ''].filter(Boolean).join(' ')}
    />
  );
}

export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select {...props} className={['select', props.className ?? ''].filter(Boolean).join(' ')} />
  );
}

export function Checkbox({
  label,
  ...props
}: InputHTMLAttributes<HTMLInputElement> & { label: string }) {
  return (
    <label className="checkbox">
      <input type="checkbox" {...props} />
      <span>{label}</span>
    </label>
  );
}

export function Field({
  label,
  children,
  hint,
  error,
  className
}: {
  label: string;
  children: ReactNode;
  hint?: string;
  error?: string;
  className?: string;
}) {
  return (
    <label
      className={['field', error ? 'has-error' : '', className ?? ''].filter(Boolean).join(' ')}
    >
      <span className="field-label">{label}</span>
      {children}
      {error ? (
        <small className="field-hint" role="alert">
          {error}
        </small>
      ) : hint ? (
        <small className="field-hint">{hint}</small>
      ) : null}
    </label>
  );
}

/* -------------------------------------------------------------------------- */
/* Containers                                                                  */
/* -------------------------------------------------------------------------- */

export function Card({
  title,
  description,
  children,
  footer,
  action,
  tight
}: {
  title?: string;
  description?: string;
  children: ReactNode;
  footer?: ReactNode;
  action?: ReactNode;
  tight?: boolean;
}) {
  return (
    <section className={['card', tight ? 'tight' : ''].filter(Boolean).join(' ')}>
      {title || description || action ? (
        <header className="card-head">
          <div>
            {title ? <h2 className="card-title">{title}</h2> : null}
            {description ? <p className="card-desc">{description}</p> : null}
          </div>
          {action}
        </header>
      ) : null}
      {children}
      {footer ? <div>{footer}</div> : null}
    </section>
  );
}

export function Panel({
  title,
  description,
  action,
  children,
  contentClassName
}: {
  title: string;
  description?: string;
  action?: ReactNode;
  children: ReactNode;
  contentClassName?: string;
}) {
  return (
    <aside className="panel">
      <header className="panel-head">
        <div className="row between">
          <div>
            <div className="panel-title">{title}</div>
            {description ? <div className="panel-desc">{description}</div> : null}
          </div>
          {action}
        </div>
      </header>
      <div className={contentClassName ?? 'panel-body'}>{children}</div>
    </aside>
  );
}

/* -------------------------------------------------------------------------- */
/* Atoms                                                                       */
/* -------------------------------------------------------------------------- */

export function Badge({
  tone = 'neutral',
  outline,
  children
}: {
  tone?: Tone;
  outline?: boolean;
  children: ReactNode;
}) {
  return (
    <span className={['badge', tone === 'neutral' ? '' : tone, outline ? 'outline' : '']
      .filter(Boolean)
      .join(' ')}>
      {children}
    </span>
  );
}

export function StatusPill({
  status,
  children
}: {
  status: 'ready' | 'draft' | 'notReady' | 'disabled' | 'saved' | 'warning';
  children: ReactNode;
}) {
  const tone: Tone =
    status === 'ready' || status === 'saved'
      ? 'success'
      : status === 'notReady' || status === 'disabled'
        ? 'danger'
        : 'warning';
  return <Badge tone={tone}>{children}</Badge>;
}

export function EmptyState({
  title,
  description,
  action
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="empty">
      <strong className="empty-title">{title}</strong>
      {description ? <p className="empty-desc">{description}</p> : null}
      {action}
    </div>
  );
}

export function InlineAlert({
  tone = 'info',
  title,
  children
}: {
  tone?: 'info' | 'success' | 'warning' | 'danger' | 'muted';
  title: string;
  children?: ReactNode;
}) {
  return (
    <div
      className={['alert', tone === 'info' ? '' : tone].filter(Boolean).join(' ')}
      role={tone === 'danger' ? 'alert' : 'status'}
    >
      <span className="alert-title">{title}</span>
      {children ? <span className="alert-body">{children}</span> : null}
    </div>
  );
}

export function Tabs({
  tabs,
  active,
  onChange
}: {
  tabs: Array<{ id: string; label: string; badge?: number }>;
  active: string;
  onChange: (id: string) => void;
}) {
  return (
    <div className="tabs" role="tablist" aria-label="侧边面板">
      {tabs.map(tab => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={active === tab.id}
          onClick={() => onChange(tab.id)}
        >
          {tab.label}
          {tab.badge ? <span className="tab-badge">{tab.badge}</span> : null}
        </button>
      ))}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Modal                                                                       */
/* -------------------------------------------------------------------------- */

export function Modal({
  title,
  description,
  children,
  onClose
}: {
  title: string;
  description?: string;
  children: ReactNode;
  onClose: () => void;
}) {
  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  return (
    <div
      className="modal-backdrop"
      role="presentation"
      onMouseDown={event => event.target === event.currentTarget && onClose()}
    >
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
        <header className="modal-head">
          <div>
            <strong id="modal-title" className="modal-title">
              {title}
            </strong>
            {description ? <p className="modal-desc">{description}</p> : null}
          </div>
          <IconButton aria-label="关闭弹窗" onClick={onClose}>
            ×
          </IconButton>
        </header>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}
