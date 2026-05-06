import { Badge, Button, EmptyState } from '../design-system/Primitives';
import type { RenderRun } from './types';

export function RenderPanel({ run, onRender, docxRenderEnabled = true }: { run?: RenderRun; onRender: () => void; docxRenderEnabled?: boolean }) {
  return (
    <div className="stack" data-testid="render-panel">
      <Button type="button" variant="primary" onClick={onRender} disabled={!docxRenderEnabled}>生成 DOCX</Button>
      {!docxRenderEnabled ? (
        <p className="muted" role="status">当前部署仅支持结构化编辑与 JSON 导出；DOCX 生成需要连接后端渲染服务。</p>
      ) : null}
      {!run ? <EmptyState title="尚未生成 DOCX。" /> : (
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
