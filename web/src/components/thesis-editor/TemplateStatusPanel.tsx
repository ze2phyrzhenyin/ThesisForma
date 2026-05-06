import { Badge, EmptyState } from '../design-system/Primitives';
import type { TemplateSummary } from './types';

export function TemplateStatusPanel({ template }: { template?: TemplateSummary }) {
  if (!template) {
    return <EmptyState title="尚未选择模板。" />;
  }

  const tone = template.status === 'ready' ? 'success' : 'warning';
  return (
    <div className="stack" data-testid="template-status-panel">
      <div className="inline-row" style={{ justifyContent: 'space-between' }}>
        <strong>{template.name}</strong>
        <Badge tone={tone}>{template.status}</Badge>
      </div>
      <p className="muted">{template.school} / {template.college}</p>
      <p>版本：{template.version}</p>
      <p>Coverage：{Math.round(template.coverage * 100)}%</p>
      <p className="muted">字体、字号、行距、页边距由模板自动控制，编辑器只录入内容和结构。</p>
    </div>
  );
}
