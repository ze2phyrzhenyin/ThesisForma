import { Badge, Button, EmptyState } from '../design-system/Primitives';
import type { ValidationIssue } from './types';

const groups = [
  { severity: 'error', title: '必须修复', tone: 'danger' as const },
  { severity: 'warning', title: '建议修复', tone: 'warning' as const },
  { severity: 'info', title: '提示', tone: 'info' as const }
] as const;

const severityLabel: Record<ValidationIssue['severity'], string> = {
  error: '错误',
  warning: '警告',
  info: '提示'
};

export function ValidationPanel({ issues, onJump }: { issues: ValidationIssue[]; onJump: (blockId?: string) => void }) {
  if (issues.length === 0) {
    return (
      <EmptyState
        title="暂无校验问题"
        description="当前结构可继续导出 JSON。正式 DOCX 仍建议通过后端 validate-input 校验。"
      />
    );
  }

  const counts = {
    error: issues.filter(issue => issue.severity === 'error').length,
    warning: issues.filter(issue => issue.severity === 'warning').length,
    info: issues.filter(issue => issue.severity === 'info').length
  };

  return (
    <div className="stack" data-testid="validation-panel">
      <div className="issue-summary" aria-label="校验汇总">
        <div className="issue-count">
          <strong>{counts.error}</strong>
          <span>错误</span>
        </div>
        <div className="issue-count">
          <strong>{counts.warning}</strong>
          <span>警告</span>
        </div>
        <div className="issue-count">
          <strong>{counts.info}</strong>
          <span>提示</span>
        </div>
      </div>

      {groups.map(group => {
        const groupIssues = issues.filter(issue => issue.severity === group.severity);
        if (groupIssues.length === 0) return null;
        return (
          <section className="issue-group" key={group.severity}>
            <h3>{group.title}</h3>
            {groupIssues.map((issue, index) => (
              <div key={`${issue.code}-${index}`} className={`issue-row ${issue.severity}`}>
                <div className="issue-row-header">
                  <Badge tone={group.tone}>{severityLabel[issue.severity]}</Badge>
                  {issue.blockId ? (
                    <Button type="button" onClick={() => onJump(issue.blockId)}>跳转到内容块</Button>
                  ) : null}
                </div>
                <strong>{issue.message}</strong>
                {issue.path ? (
                  <span className="muted">位置：{issue.path}</span>
                ) : issue.blockId ? (
                  <span className="muted">内容块：{issue.blockId}</span>
                ) : null}
                {issue.suggestedAction ? (
                  <p className="muted">建议：{issue.suggestedAction}</p>
                ) : null}
              </div>
            ))}
          </section>
        );
      })}
    </div>
  );
}
