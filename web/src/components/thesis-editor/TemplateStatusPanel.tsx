import { Badge, EmptyState, InlineAlert, StatusPill } from '../design-system/Primitives';
import type { TemplateSummary } from './types';

export function TemplateStatusPanel({ template }: { template?: TemplateSummary }) {
  if (!template) {
    return <EmptyState title="尚未选择模板。" />;
  }

  const tone = template.status === 'ready' ? 'success' : 'warning';
  return (
    <div className="stack" data-testid="template-status-panel">
      <div className="inline-row">
        <strong>{template.name}</strong>
        <StatusPill status={template.status === 'ready' ? 'ready' : template.status === 'notReady' ? 'notReady' : 'draft'}>{template.status}</StatusPill>
      </div>
      <p className="muted">{template.school} / {template.college}</p>
      <div className="inline-row">
        <Badge>版本 {template.version}</Badge>
        <Badge tone={tone}>{Math.round(template.coverage * 100)}% 覆盖</Badge>
      </div>
      <InlineAlert title="格式由模板控制">
        字体、字号、行距、页边距、图表题注和目录样式都不在网页中手工设置。
      </InlineAlert>
      {template.status !== 'ready' ? (
        <InlineAlert tone="warning" title="模板仍是草稿">
          仍可能存在未映射格式要求，请先导出 JSON 或连接后端质量门禁再生成 DOCX。
        </InlineAlert>
      ) : null}
    </div>
  );
}
