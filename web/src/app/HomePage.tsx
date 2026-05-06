import { useEffect, useRef, useState } from 'react';
import { Badge, Button, Card, EmptyState, InlineAlert } from '../components/design-system/Primitives';

type DraftEntry = {
  id: string;
  title: string;
  templateId: string;
  updatedAt: string;
};

function loadDrafts(): DraftEntry[] {
  const entries: DraftEntry[] = [];
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (!key?.startsWith('thesisforma.document.')) continue;
    try {
      const raw = localStorage.getItem(key);
      if (!raw) continue;
      const envelope = JSON.parse(raw) as { id?: string; document?: { metadata?: { title?: string } }; templateId?: string; updatedAt?: string };
      entries.push({
        id: envelope.id ?? key.replace('thesisforma.document.', ''),
        title: envelope.document?.metadata?.title?.trim() || '未命名论文',
        templateId: envelope.templateId ?? '',
        updatedAt: envelope.updatedAt ?? ''
      });
    } catch {
      // Skip malformed entries
    }
  }
  return entries.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt)).slice(0, 8);
}

function formatDate(iso: string) {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString('zh-CN', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

export function HomePage({
  onNew,
  onTemplates,
  onImportJson,
  onOpenDraft
}: {
  onNew: () => void;
  onTemplates: () => void;
  onImportJson?: (file: File) => void;
  onOpenDraft?: (draftId: string) => void;
}) {
  const importRef = useRef<HTMLInputElement>(null);
  const [drafts, setDrafts] = useState<DraftEntry[]>([]);

  useEffect(() => {
    setDrafts(loadDrafts());
  }, []);

  return (
    <div className="app-shell">
      <header className="topbar topbar-home">
        <div className="brand">
          <span className="brand-title">ThesisForma</span>
          <span className="brand-subtitle">论文结构化编辑器</span>
        </div>
      </header>
      <main className="home-page">
        <section className="home-hero-v2">
          <div className="home-hero-copy">
            <div className="home-eyebrow">
              <span>STRUCTURED THESIS WORKSPACE</span>
              <Badge tone="info">Frontend MVP</Badge>
            </div>
            <h1>把论文内容写成稳定结构，而不是在网页里排版。</h1>
            <p>
              ThesisForma 让学生录入标题、段落、图表、脚注和参考文献；学院格式由模板控制，
              导出稳定的 ThesisDocument JSON 后再交给 OpenXML 渲染服务生成 DOCX。
            </p>
            <div className="hero-actions">
              <Button variant="primary" onClick={onNew}>新建论文</Button>
              <Button onClick={onTemplates}>选择模板</Button>
              <Button onClick={() => importRef.current?.click()}>导入 JSON</Button>
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
          <aside className="workflow-panel" aria-label="工作流程">
            <div className="workflow-row active">
              <span>01</span>
              <strong>录入结构</strong>
              <p>metadata、section、block、引用和参考文献。</p>
            </div>
            <div className="workflow-row">
              <span>02</span>
              <strong>模板约束</strong>
              <p>格式规则从 TemplatePackage 读取，不在前端手调样式。</p>
            </div>
            <div className="workflow-row">
              <span>03</span>
              <strong>导出 JSON</strong>
              <p>前端模式可直接下载 ThesisDocument JSON。</p>
            </div>
            <div className="workflow-row muted-row">
              <span>04</span>
              <strong>后端生成 DOCX</strong>
              <p>连接 .NET OpenXML 服务后启用。</p>
            </div>
          </aside>
        </section>

        <div className="home-mode-strip">
          <strong>当前线上模式</strong>
          <span>支持结构化编辑、本地保存、导入/导出 JSON；DOCX 生成需要连接后端渲染服务。</span>
        </div>

        <div className="home-workspace-grid">
          <Card title="最近草稿" description="草稿保存在浏览器本地，不写入公开仓库。">
              {drafts.length === 0 ? (
                <EmptyState
                  title="暂无最近草稿"
                  description="点击新建论文，先填写题目、作者和正文结构。"
                  action={<Button variant="primary" onClick={onNew}>开始新建</Button>}
                />
              ) : (
                <div className="draft-list">
                  {drafts.map(draft => (
                    <button
                      key={draft.id}
                      type="button"
                      className="draft-item"
                      onClick={() => onOpenDraft?.(draft.id)}
                      aria-label={`打开草稿：${draft.title}`}
                    >
                      <span className="draft-title">{draft.title}</span>
                      <span className="draft-meta">{formatDate(draft.updatedAt)}</span>
                    </button>
                  ))}
                  <div className="draft-footer">
                    <Button variant="ghost" onClick={onNew}>新建论文</Button>
                  </div>
                </div>
              )}
          </Card>

          <div className="home-side-cards">
            <InlineAlert title="不是 Word 替代品">
              字体、字号、行距、页边距和页眉页脚由模板与后端渲染器控制，避免手工排版漂移。
            </InlineAlert>
            <Card title="交付物" description="前端优先保证结构可靠。">
              <div className="deliverable-list">
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
