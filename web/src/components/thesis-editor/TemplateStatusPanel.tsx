import { Badge, EmptyState, InlineAlert, StatusPill } from '../ui/Primitives';
import type { TemplateSummary } from './types';

export function TemplateStatusPanel({ template }: { template?: TemplateSummary }) {
  if (!template) {
    return <EmptyState title="尚未选择模板。" />;
  }

  return (
    <div className="stack" data-testid="template-status-panel">
      <div className="row between">
        <div className="stack tight">
          <strong>{template.name}</strong>
          <span className="muted helper">
            {template.school} · {template.college}
          </span>
        </div>
        <StatusPill
          status={
            template.status === 'ready' ? 'ready' : template.status === 'notReady' ? 'notReady' : 'draft'
          }
        >
          {template.status}
        </StatusPill>
      </div>

      <div className="row">
        <Badge outline>版本 {template.version}</Badge>
        <Badge tone={template.status === 'ready' ? 'success' : 'warning'}>
          {Math.round(template.coverage * 100)}% 覆盖
        </Badge>
      </div>

      <div className="template-status-grid" aria-label="模板状态详情">
        <div>
          <span>Readiness</span>
          <strong>{template.readiness}</strong>
        </div>
        <div>
          <span>Known gaps</span>
          <strong>{template.status === 'ready' ? '0' : '待确认'}</strong>
        </div>
        <div>
          <span>前端支持</span>
          <strong>结构编辑 / JSON</strong>
        </div>
      </div>

      <InlineAlert title="格式由模板控制">
        字体、字号、行距、页边距、图表题注和目录样式都不在网页中手工设置。
      </InlineAlert>

      <InlineAlert tone="muted" title="当前模板限制">
        前端只提示模板状态，不在浏览器里执行 OpenXML 渲染、格式门禁或 DOCX 视觉检查。
      </InlineAlert>

      {template.status !== 'ready' ? (
        <InlineAlert tone="warning" title="模板仍是草稿">
          仍可能存在未映射格式要求，请先导出 JSON 或连接后端质量门禁再生成 DOCX。
        </InlineAlert>
      ) : null}
    </div>
  );
}
