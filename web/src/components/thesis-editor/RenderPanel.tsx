import { Badge, Button, EmptyState, InlineAlert } from '../design-system/Primitives';
import type { RenderRun } from './types';

export function RenderPanel({ run, onRender, docxRenderEnabled = true }: { run?: RenderRun; onRender: () => void; docxRenderEnabled?: boolean }) {
  return (
    <div className="stack" data-testid="render-panel">
      <Button type="button" variant={docxRenderEnabled ? 'primary' : 'secondary'} onClick={onRender} disabled={!docxRenderEnabled}>生成 DOCX</Button>
      {!docxRenderEnabled ? (
        <InlineAlert tone="warning" title="生成 DOCX 需要后端服务">
          当前部署仅支持结构化编辑与 JSON 导出；连接 .NET OpenXML 渲染 API 后可启用在线生成。
        </InlineAlert>
      ) : null}
      {!run ? <EmptyState title="尚未生成 DOCX" description="在前端模式下，请优先导出 ThesisDocument JSON。" /> : (
        <div className="stack">
          <div className="inline-row">
            <Badge tone={run.status === 'valid' ? 'success' : 'danger'}>{run.status}</Badge>
            <span>Run: {run.runId}</span>
          </div>
          <p>OpenXML：{run.openXmlValid ? '通过' : '失败'}</p>
          <p>格式验证：{run.formatValid ? '通过' : '失败'}</p>
          {run.issues?.map(issue => <p key={issue.code} className="muted">{issue.message}</p>)}
          {run.downloadUrl ? <a href={run.downloadUrl}>下载 DOCX</a> : null}
        </div>
      )}
    </div>
  );
}
