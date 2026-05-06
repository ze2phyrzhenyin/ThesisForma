import { Badge, Button } from '../design-system/Primitives';
import type { ThesisEditorState } from './types';

export function EditorToolbar({ state, onSave, onValidate, onRender, onExportJson, onImportJson, docxRenderEnabled = true }: {
  state: ThesisEditorState;
  onSave: () => void;
  onValidate: () => void;
  onRender: () => void;
  onExportJson?: () => void;
  onImportJson?: () => void;
  docxRenderEnabled?: boolean;
}) {
  const validationTone = state.validationIssues.some(issue => issue.severity === 'error') ? 'danger' : state.validationIssues.length ? 'warning' : 'success';
  return (
    <header className="topbar">
      <div className="brand">
        <span className="brand-title">ThesisForma 结构化论文编辑器</span>
        <span className="brand-subtitle">格式由模板控制，网页只编辑内容和结构</span>
      </div>
      <div className="toolbar-actions">
        <Badge>{autosaveLabel(state.autosaveStatus)}</Badge>
        <Badge tone={validationTone}>{state.validationIssues.length ? `${state.validationIssues.length} 校验项` : '校验通过'}</Badge>
        <Button type="button" onClick={onSave}>保存</Button>
        <Button type="button" onClick={onExportJson}>导出 JSON</Button>
        <Button type="button" onClick={onImportJson}>导入 JSON</Button>
        <Button type="button" onClick={onValidate}>校验</Button>
        <Button type="button" variant="primary" onClick={onRender} disabled={!docxRenderEnabled}>生成 DOCX</Button>
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
