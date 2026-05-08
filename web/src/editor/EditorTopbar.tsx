import { useState } from 'react';
import { Brand } from '@/components/Brand';
import { useEditorActions, useEditorStore } from './EditorContext';
import { useSaveDocument, useValidateDocument } from '@/api/queries';
import { isApiBacked, ThesisApiError } from '@/api/client';
import { ExportModal } from './ExportModal';
import type { ApiIssue } from '@/types';
import {
  cleanThesisDocument,
  downloadJson,
  exportFileNameForDocument,
  validateThesisDocument
} from './documentContract';
import styles from './EditorTopbar.module.css';

interface Props {
  onValidationResult(result: { isValid: boolean | null; issues: ApiIssue[]; checkedAt: string | null }): void;
  onOpenValidationDrawer(): void;
  focusMode: boolean;
  onToggleFocus(): void;
}

export function EditorTopbar({
  onValidationResult,
  onOpenValidationDrawer,
  focusMode,
  onToggleFocus
}: Props) {
  const envelope = useEditorStore((s) => s.envelope);
  const dirty = useEditorStore((s) => s.dirty);
  const lastSavedAt = useEditorStore((s) => s.lastSavedAt);
  const actions = useEditorActions();

  const save = useSaveDocument();
  const validate = useValidateDocument();

  const [exportOpen, setExportOpen] = useState(false);
  const [exportMenuOpen, setExportMenuOpen] = useState(false);

  const runValidate = async () => {
    try {
      if (dirty) {
        const saved = await save.mutateAsync({
          id: envelope.id,
          document: envelope.document,
          templateId: envelope.templateId ?? null
        });
        actions.markSaved(saved.updatedAt);
      }
      const v = await validate.mutateAsync({
        id: envelope.id,
        templateId: envelope.templateId ?? null
      });
      onValidationResult({
        isValid: v.isValid,
        issues: v.issues,
        checkedAt: new Date().toLocaleTimeString()
      });
      onOpenValidationDrawer();
    } catch (e) {
      const msg = e instanceof ThesisApiError ? e.payload?.message ?? e.message : (e as Error).message;
      onValidationResult({
        isValid: false,
        issues: [
          {
            code: 'request.failed',
            message: msg,
            severity: 'error',
            path: null,
            suggestedAction: null
          }
        ],
        checkedAt: new Date().toLocaleTimeString()
      });
      onOpenValidationDrawer();
    }
  };

  const onExportJson = async () => {
    try {
      const cleaned = cleanThesisDocument(envelope.document);
      const issues = validateThesisDocument(cleaned);
      const errors = issues.filter((item) => item.severity === 'error');
      const warnings = issues.filter((item) => item.severity === 'warning');
      if (errors.length) {
        onValidationResult({
          isValid: false,
          issues,
          checkedAt: new Date().toLocaleTimeString()
        });
        onOpenValidationDrawer();
        return;
      }
      if (warnings.length) {
        const proceed = window.confirm(`导出前发现 ${warnings.length} 个警告。仍然导出 ThesisDocument JSON？`);
        if (!proceed) {
          onValidationResult({
            isValid: false,
            issues,
            checkedAt: new Date().toLocaleTimeString()
          });
          onOpenValidationDrawer();
          return;
        }
      }
      if (dirty) {
        const saved = await save.mutateAsync({
          id: envelope.id,
          document: cleaned,
          templateId: envelope.templateId ?? null
        });
        actions.markSaved(saved.updatedAt);
      }
      downloadJson(exportFileNameForDocument(cleaned), cleaned);
    } finally {
      setExportMenuOpen(false);
    }
  };

  const onExportDocx = () => {
    setExportMenuOpen(false);
    setExportOpen(true);
  };

  const savedLabel = (() => {
    if (save.isPending) return '保存中…';
    if (dirty) return '未保存';
    if (lastSavedAt) {
      try {
        const d = new Date(lastSavedAt);
        return `已保存 · ${d.toLocaleTimeString()}`;
      } catch {
        return '已保存';
      }
    }
    return '已保存';
  })();

  return (
    <header className={styles.topbar}>
      <div className={styles.left}>
        <Brand size="sm" />
        {envelope.templateId && (
          <span className={styles.templateBadge} title={`模板：${envelope.templateId}`}>
            {envelope.templateId}
          </span>
        )}
      </div>

      <div className={styles.center}>
        <input
          className={styles.titleInput}
          value={envelope.document.metadata.title}
          onChange={(e) => actions.updateMetadata({ title: e.target.value })}
          placeholder="未命名论文"
          aria-label="论文题目"
        />
      </div>

      <div className={styles.right}>
        <span className={styles.savedTag} data-dirty={dirty}>
          {savedLabel}
        </span>
        <button
          type="button"
          className={styles.btnGhost}
          onClick={onToggleFocus}
          title="专注模式 (⌘.)"
          aria-pressed={focusMode}
        >
          {focusMode ? '退出专注' : '专注'}
        </button>
        <button
          type="button"
          className={styles.btnGhost}
          onClick={runValidate}
          disabled={validate.isPending}
        >
          {validate.isPending ? '校验中…' : '校验'}
        </button>
        <div className={styles.exportWrap}>
          <button
            type="button"
            className={styles.btnPrimary}
            onClick={() => setExportMenuOpen((v) => !v)}
            aria-haspopup="menu"
            aria-expanded={exportMenuOpen}
          >
            导出 ▾
          </button>
          {exportMenuOpen && (
            <div
              className={styles.exportMenu}
              role="menu"
              onMouseLeave={() => setExportMenuOpen(false)}
            >
              <button type="button" role="menuitem" onClick={onExportDocx} disabled={!isApiBacked}>
                <strong>DOCX</strong>
                <span>{isApiBacked ? '渲染并下载 .docx' : '静态前端暂不渲染 DOCX'}</span>
              </button>
              <button type="button" role="menuitem" onClick={onExportJson}>
                <strong>JSON</strong>
                <span>导出结构化文档</span>
              </button>
            </div>
          )}
        </div>
      </div>

      <ExportModal open={exportOpen} onClose={() => setExportOpen(false)} />
    </header>
  );
}
