import { useEffect, useRef, useState } from 'react';
import { Badge, Button, Card, EmptyState, InlineAlert } from '../components/ui/Primitives';
import { deleteLocalDraft, loadLocalDrafts, type LocalDraftEntry } from '../components/thesis-editor/localDraftStorage';

function formatDate(iso: string) {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString('zh-CN', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  } catch {
    return iso;
  }
}

export function HomePage({
  onNew,
  onTemplates,
  onImportJson,
  onOpenDraft,
  notice
}: {
  onNew: () => void;
  onTemplates: () => void;
  onImportJson?: (file: File) => void;
  onOpenDraft?: (draftId: string) => void;
  notice?: { tone: 'success' | 'danger'; title: string; message: string };
}) {
  const importRef = useRef<HTMLInputElement>(null);
  const [drafts, setDrafts] = useState<LocalDraftEntry[]>([]);

  useEffect(() => {
    setDrafts(loadLocalDrafts());
  }, []);

  function removeDraft(draftId: string) {
    if (!window.confirm('删除这个本地草稿？该操作不会影响已导出的 JSON 文件。')) return;
    deleteLocalDraft(draftId);
    setDrafts(loadLocalDrafts());
  }

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark">ThesisForma</span>
          <span className="brand-sub">论文结构化编辑器</span>
        </div>
        <div className="toolbar-spacer" />
        <Button onClick={onTemplates}>模板库</Button>
      </header>

      <main className="home">
        <section className="hero">
          <div className="hero-copy">
            <span className="eyebrow">
              Structured thesis workspace
              <Badge tone="info">Frontend MVP</Badge>
            </span>
            <h1>把论文写成结构，而不是在网页里排版。</h1>
            <p className="hero-lede">
              ThesisForma
              让作者专注于标题、段落、图表、脚注与参考文献等内容结构；学院格式由模板控制，
              导出稳定的 ThesisDocument JSON 后再交给 OpenXML 渲染服务生成 DOCX。
            </p>
            <div className="hero-actions">
              <Button variant="primary" size="lg" onClick={onNew}>
                新建论文
              </Button>
              <Button size="lg" onClick={onTemplates}>
                选择模板
              </Button>
              <Button size="lg" onClick={() => importRef.current?.click()}>
                导入 JSON
              </Button>
              <input
                ref={importRef}
                data-home-import
                type="file"
                accept="application/json,.json"
                hidden
                aria-label="导入 JSON 草稿"
                onChange={event => {
                  const file = event.target.files?.[0];
                  if (file) onImportJson?.(file);
                  event.target.value = '';
                }}
              />
            </div>
          </div>

          <aside className="workflow" aria-label="工作流程">
            <div className="workflow-title">工作流</div>
            <div className="workflow-step">
              <span className="step-num">01</span>
              <div>
                <strong>录入结构</strong>
                <p>metadata · section · block · 引用 · 参考文献。</p>
              </div>
            </div>
            <div className="workflow-step">
              <span className="step-num">02</span>
              <div>
                <strong>模板约束</strong>
                <p>格式规则从 TemplatePackage 读取，不在前端手调样式。</p>
              </div>
            </div>
            <div className="workflow-step">
              <span className="step-num">03</span>
              <div>
                <strong>导出 JSON</strong>
                <p>前端模式可直接下载 ThesisDocument JSON 草稿。</p>
              </div>
            </div>
            <div className="workflow-step muted">
              <span className="step-num">04</span>
              <div>
                <strong>后端生成 DOCX</strong>
                <p>连接 .NET OpenXML 渲染服务后启用。</p>
              </div>
            </div>
          </aside>
        </section>

        <div className="mode-strip">
          <strong>当前线上模式</strong>
          <span>支持结构化编辑、本地保存、导入/导出 JSON；DOCX 生成需要连接后端渲染服务。</span>
        </div>

        {notice ? (
          <div className="home-notice">
            <InlineAlert tone={notice.tone} title={notice.title}>
              {notice.message}
            </InlineAlert>
          </div>
        ) : null}

        <div className="home-grid">
          <Card title="最近草稿" description="草稿保存在浏览器本地，不会写入公开仓库。">
            {drafts.length === 0 ? (
              <EmptyState
                title="暂无最近草稿"
                description="点击新建论文，先填写题目、作者和正文结构。"
                action={
                  <Button variant="primary" onClick={onNew}>
                    开始新建
                  </Button>
                }
              />
            ) : (
              <div className="draft-list">
                {drafts.map(draft => (
                  <div
                    key={draft.id}
                    className="draft-item"
                  >
                    <button
                      type="button"
                      className="draft-open"
                      onClick={() => onOpenDraft?.(draft.id)}
                      aria-label={`打开草稿：${draft.title}`}
                    >
                      <span className="draft-title">{draft.title}</span>
                      <span className="draft-meta">{formatDate(draft.updatedAt)}</span>
                    </button>
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      onClick={() => removeDraft(draft.id)}
                      aria-label={`删除草稿：${draft.title}`}
                    >
                      删除
                    </Button>
                  </div>
                ))}
              </div>
            )}
          </Card>

          <div className="home-side">
            <InlineAlert title="不是 Word 替代品">
              字体、字号、行距、页边距和页眉页脚由模板与后端渲染器控制，避免手工排版漂移。
            </InlineAlert>
            <Card title="交付物" description="前端优先保证结构可靠。">
              <div className="deliverables">
                <span>ThesisDocument JSON</span>
                <span>结构校验问题</span>
                <span>模板状态提示</span>
              </div>
            </Card>
          </div>
        </div>
      </main>
    </div>
  );
}
