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
  const validationTone = state.validationIssues.some(issue => issue.severity === 'error') ? 'danger' : state.validationIssues.length ? 'warning' : 'success';
  const title = state.metadata.title.trim() || '未命名论文';
  return (
    <header className="topbar">
      <div className="brand">
        <span className="brand-title">ThesisForma</span>
        <span className="brand-subtitle">{title}</span>
      </div>
      <nav className="toolbar-nav" aria-label="页面导航">
        <Button type="button" onClick={onHome}>首页</Button>
        <Button type="button" onClick={onTemplates}>模板</Button>
        <Button type="button" onClick={onBack}>后退</Button>
      </nav>
      <div className="toolbar-center">
        <Badge tone="info">当前模板：{state.template?.name ?? state.templateId}</Badge>
        {!docxRenderEnabled ? <StatusPill status="disabled">前端模式：仅导出 JSON</StatusPill> : null}
      </div>
      <div className="toolbar-actions">
        <Badge>{autosaveLabel(state.autosaveStatus)}</Badge>
        <Badge tone={validationTone}>{state.validationIssues.length ? `${state.validationIssues.length} 校验项` : '校验通过'}</Badge>
        <Button type="button" onClick={onUndo} disabled={!canUndo}>撤销</Button>
        <Button type="button" onClick={onRedo} disabled={!canRedo}>重做</Button>
        <Button type="button" onClick={onSave}>保存</Button>
        <Button type="button" onClick={onImportJson}>导入 JSON</Button>
        <Button type="button" onClick={onValidate}>校验</Button>
        <Button type="button" variant="primary" onClick={onExportJson}>导出 JSON</Button>
        <Button type="button" onClick={onRender} disabled={!docxRenderEnabled} aria-label={docxRenderEnabled ? '生成 DOCX' : '生成 DOCX 需要连接后端渲染服务'}>生成 DOCX</Button>
      </div>
    </header>
  );
}

function autosaveLabel(status: ThesisEditorState['autosaveStatus']) {
  return {
    unsaved: '未保存',
    saving: '保存中',
    saved: '已保存',
    failed: '保存失败'
  }[status];
}
