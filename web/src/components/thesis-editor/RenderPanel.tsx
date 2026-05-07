import { Badge, Button, EmptyState, InlineAlert } from '../ui/Primitives';
import type { RenderRun } from './types';

export function RenderPanel({
  run,
  onRender,
  docxRenderEnabled = true
}: {
  run?: RenderRun;
  onRender: () => void;
  docxRenderEnabled?: boolean;
}) {
  return (
    <div className="stack" data-testid="render-panel">
      <Button
        type="button"
        variant={docxRenderEnabled ? 'primary' : 'secondary'}
        onClick={onRender}
        disabled={!docxRenderEnabled}
      >
        生成 DOCX
      </Button>

      {!docxRenderEnabled ? (
        <InlineAlert tone="warning" title="生成 DOCX 需要后端服务">
          当前部署仅支持结构化编辑与 JSON 导出；连接 .NET OpenXML 渲染 API 后可启用在线生成。
        </InlineAlert>
      ) : null}

      {!run ? (
        <EmptyState
          title="尚未生成 DOCX"
          description="在前端模式下，请优先导出 ThesisDocument JSON。"
        />
      ) : (
        <div className="stack">
          <div className="row">
            <Badge tone={run.status === 'valid' ? 'success' : 'danger'}>{run.status}</Badge>
            <span className="muted helper">Run: {run.runId}</span>
          </div>
          <div className="run-summary">
            <div className={`run-check ${run.openXmlValid ? 'ok' : 'fail'}`}>
              <span>{run.openXmlValid ? '✓' : '✗'}</span>
              <span>OpenXML</span>
            </div>
            <div className={`run-check ${run.formatValid ? 'ok' : 'fail'}`}>
              <span>{run.formatValid ? '✓' : '✗'}</span>
              <span>格式校验</span>
            </div>
          </div>
          {run.issues?.map(issue => (
            <p key={issue.code} className="helper">
              {issue.message}
            </p>
          ))}
          {run.downloadUrl ? (
            <a className="download-link" href={run.downloadUrl}>
              下载 DOCX
            </a>
          ) : null}
        </div>
      )}
    </div>
  );
}
