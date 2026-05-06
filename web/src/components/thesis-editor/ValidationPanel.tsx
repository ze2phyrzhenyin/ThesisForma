import { Badge, Button, EmptyState } from '../design-system/Primitives';
import type { ValidationIssue } from './types';

export function ValidationPanel({ issues, onJump }: { issues: ValidationIssue[]; onJump: (blockId?: string) => void }) {
  if (issues.length === 0) {
    return <EmptyState title="暂无校验问题。" />;
  }

  return (
    <div className="stack" data-testid="validation-panel">
      {issues.map((issue, index) => (
        <div key={`${issue.code}-${index}`} className={`issue-row ${issue.severity}`}>
          <div className="inline-row" style={{ justifyContent: 'space-between' }}>
            <Badge tone={issue.severity === 'error' ? 'danger' : issue.severity === 'warning' ? 'warning' : 'neutral'}>{issue.severity}</Badge>
            {issue.blockId ? <Button type="button" onClick={() => onJump(issue.blockId)}>跳转</Button> : null}
          </div>
          <strong>{issue.message}</strong>
          {issue.suggestedAction ? <p className="muted">{issue.suggestedAction}</p> : null}
        </div>
      ))}
    </div>
  );
}
