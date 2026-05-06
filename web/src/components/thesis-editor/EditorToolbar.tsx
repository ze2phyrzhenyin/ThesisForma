import { Badge, Button, StatusPill } from '../design-system/Primitives';
import type { ThesisEditorState } from './types';

export function EditorToolbar({
  state,
  onSave,
  onValidate,
  onRender,
  onExportJson,
  onImportJson,
  onHome,
  onTemplates,
  onBack,
  onUndo,
  onRedo,
  canUndo = false,
  canRedo = false,
  docxRenderEnabled = true
}: {
  state: ThesisEditorState;
  onSave: () => void;
  onValidate: () => void;
  onRender: () => void;
  onExportJson?: () => void;
  onImportJson?: () => void;
  onHome?: () => void;
  onTemplates?: () => void;
  onBack?: () => void;
  onUndo?: () => void;
  onRedo?: () => void;
  canUndo?: boolean;
  canRedo?: boolean;
  docxRenderEnabled?: boolean;
}) {
  const issueCount = state.validationIssues.length;
  const hasErrors = state.validationIssues.some(issue => issue.severity === 'error');
  const title = state.metadata.title.trim() || '未命名论文';

  return (
    <header className="topbar topbar-editor" role="banner">
      <div className="toolbar-brand">
        <span className="brand-title">ThesisForma</span>
        <span className="brand-doc-title" title={title}>{title}</span>
      </div>

      <nav className="toolbar-nav" aria-label="页面导航">
        <Button type="button" onClick={onHome} aria-label="首页">首页</Button>
        <Button type="button" onClick={onTemplates} aria-label="模板">模板</Button>
        <Button type="button" onClick={onBack} aria-label="后退">后退</Button>
      </nav>

      <div className="toolbar-divider" aria-hidden="true" />

      <div className="toolbar-history" aria-label="撤销重做">
        <Button
          type="button"
          onClick={onUndo}
          disabled={!canUndo}
          aria-label="撤销"
          title="撤销 (Cmd+Z)"
        >
          撤销
        </Button>
        <Button
          type="button"
          onClick={onRedo}
          disabled={!canRedo}
          aria-label="重做"
          title="重做 (Cmd+Shift+Z)"
        >
          重做
        </Button>
      </div>

      <div className="toolbar-divider" aria-hidden="true" />

      <div className="toolbar-status" aria-live="polite">
        <span className={`autosave-label autosave-${state.autosaveStatus}`}>
          {autosaveLabel(state.autosaveStatus)}
        </span>
        {!docxRenderEnabled ? (
          <StatusPill status="disabled">仅 JSON 导出</StatusPill>
        ) : null}
        <Badge tone={issueCount > 0 ? (hasErrors ? 'danger' : 'warning') : 'success'}>
          {issueCount > 0 ? `${issueCount} 校验项` : '校验通过'}
        </Badge>
        <Badge tone="info">{state.template?.name ?? state.templateId}</Badge>
      </div>

      <div className="toolbar-actions">
        <Button type="button" onClick={onSave} aria-label="保存">保存</Button>
        <Button type="button" onClick={onValidate} aria-label="校验">校验</Button>
        <div className="toolbar-divider" aria-hidden="true" />
        <Button type="button" onClick={onImportJson} aria-label="导入 JSON">导入 JSON</Button>
        <Button type="button" variant="primary" onClick={onExportJson} aria-label="导出 JSON">导出 JSON</Button>
        <div className="toolbar-divider" aria-hidden="true" />
        <Button
          type="button"
          onClick={onRender}
          disabled={!docxRenderEnabled}
          aria-label={docxRenderEnabled ? '生成 DOCX' : '生成 DOCX 需要连接后端渲染服务'}
          title={docxRenderEnabled ? '生成 DOCX' : '生成 DOCX 需要连接后端渲染服务'}
        >
          生成 DOCX
        </Button>
      </div>
    </header>
  );
}

function autosaveLabel(status: ThesisEditorState['autosaveStatus']) {
  return {
    unsaved: '未保存',
    saving: '保存中…',
    saved: '已保存',
    failed: '保存失败'
  }[status];
}
