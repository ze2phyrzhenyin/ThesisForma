import { Badge, Button, Card } from '../components/design-system/Primitives';
import type { RenderRun } from '../components/thesis-editor/types';

export function RunPage({ run, onBack }: { run?: RenderRun; onBack: () => void }) {
  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <span className="brand-title">生成结果</span>
          <span className="brand-subtitle">查看 render / validate / inspect 摘要并下载 DOCX</span>
        </div>
        <Button onClick={onBack}>返回编辑器</Button>
      </header>
      <main className="page">
        <div className="landing-grid">
          <Card title="运行状态">
            {run ? (
              <div className="stack">
                <Badge tone={run.status === 'valid' ? 'success' : 'danger'}>{run.status}</Badge>
                <p>Run ID: {run.runId}</p>
                <p>OpenXML: {run.openXmlValid ? '通过' : '失败'}</p>
                <p>格式验证: {run.formatValid ? '通过' : '失败'}</p>
                <a href={run.downloadUrl}>下载生成的 DOCX</a>
              </div>
            ) : <p className="muted">尚未生成运行结果。</p>}
          </Card>
          <Card title="错误和修复建议">
            {run?.issues?.length ? run.issues.map(issue => (
              <div key={issue.code} className={`issue-row ${issue.severity}`}>
                <strong>{issue.message}</strong>
                {issue.suggestedAction ? <p>{issue.suggestedAction}</p> : null}
              </div>
            )) : <p className="muted">没有结构化错误。</p>}
          </Card>
        </div>
      </main>
    </div>
  );
}
