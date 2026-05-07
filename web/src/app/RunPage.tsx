import { Badge, Button, Card, InlineAlert } from '../components/ui/Primitives';
import type { RenderRun } from '../components/thesis-editor/types';

export function RunPage({ run, onBack, onHome }: { run?: RenderRun; onBack: () => void; onHome?: () => void }) {
  return (
    <div className="app">
      <header className="topbar">
        <button
          type="button"
          className="brand brand-home-button"
          onClick={onHome}
          aria-label="ThesisForma 返回首页"
          title="返回首页"
        >
          <span className="brand-mark">ThesisForma</span>
          <span className="brand-sub">生成结果</span>
        </button>
        <div className="toolbar-spacer" />
        <Button type="button" onClick={onBack}>
          返回编辑器
        </Button>
      </header>

      <main className="page">
        <div className="stack">
          <InlineAlert tone="warning" title="当前部署：前端模式">
            Vercel 部署仅支持结构化编辑与 JSON 导出。在线生成 DOCX 需要连接后端 .NET OpenXML
            渲染服务。请优先使用编辑器中的"导出 JSON"按钮。
          </InlineAlert>

          <div
            className="home-grid"
            style={{ gridTemplateColumns: 'minmax(0, 1fr) minmax(0, 1fr)' }}
          >
            <Card title="运行状态">
              {run ? (
                <div className="stack">
                  <div className="row">
                    <Badge
                      tone={
                        run.status === 'valid'
                          ? 'success'
                          : run.status === 'disabled'
                            ? 'neutral'
                            : 'danger'
                      }
                    >
                      {runStatusLabel(run.status)}
                    </Badge>
                    <span className="muted">运行 ID：{run.runId}</span>
                  </div>
                  <div className="run-summary">
                    <div className={`run-check ${run.openXmlValid ? 'ok' : 'fail'}`}>
                      <span>{run.openXmlValid ? '✓' : '✗'}</span>
                      <span>OpenXML 结构验证</span>
                    </div>
                    <div className={`run-check ${run.formatValid ? 'ok' : 'fail'}`}>
                      <span>{run.formatValid ? '✓' : '✗'}</span>
                      <span>格式规范验证</span>
                    </div>
                  </div>
                  {run.downloadUrl ? (
                    <a href={run.downloadUrl} className="download-link">
                      下载 DOCX
                    </a>
                  ) : null}
                </div>
              ) : (
                <p className="muted">
                  尚未生成运行结果。请先在编辑器中使用"生成 DOCX"（需要后端服务）。
                </p>
              )}
            </Card>

            <Card title="校验问题">
              {run?.issues?.length ? (
                <div className="stack">
                  {run.issues.map((issue, i) => (
                    <div key={`${issue.code}-${i}`} className={`issue ${issue.severity}`}>
                      <strong>{issue.message}</strong>
                      {issue.suggestedAction ? (
                        <span className="issue-meta">建议：{issue.suggestedAction}</span>
                      ) : null}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="muted">没有结构化错误。</p>
              )}
            </Card>
          </div>
        </div>
      </main>
    </div>
  );
}

function runStatusLabel(status: string) {
  const labels: Record<string, string> = {
    valid: '通过',
    invalid: '验证失败',
    disabled: '前端模式 — 未生成',
    failed: '生成失败'
  };
  return labels[status] ?? status;
}
