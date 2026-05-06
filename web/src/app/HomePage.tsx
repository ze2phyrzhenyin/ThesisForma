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
          <span className="brand-subtitle">结构化录入论文内容，模板决定 DOCX 格式</span>
        </div>
      </header>
      <main className="page">
        <div className="home-shell">
          <section className="hero-panel">
            <div className="hero-badges">
              <Badge tone="info">Frontend MVP</Badge>
              <Badge>结构化编辑</Badge>
            </div>
            <h1>论文结构化编辑器</h1>
            <p>录入论文结构内容，选择模板，导出可渲染的 ThesisDocument JSON。格式由模板统一控制，网页不提供手工字体、字号、行距和页边距设置。</p>
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
          </section>

          <InlineAlert title="Vercel 前端模式">
            当前线上版本支持结构化编辑、本地保存、导入 JSON 和导出 JSON；DOCX 生成需要连接后端 OpenXML 渲染服务。
          </InlineAlert>

          <div className="home-two-col">
            <Card title="最近草稿" description="草稿保存在浏览器本地存储中，不写入公开仓库。">
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

            <div className="home-info-stack">
              <Card title="结构化工作方式" description="用户只负责内容和结构。">
                <p className="muted">封面、声明页、目录、页眉页脚、字体字号和行距由模板与后端渲染器控制，避免手工排版漂移。</p>
              </Card>
              <Card title="导出与后端渲染" description="前端先产出稳定 JSON。">
                <p className="muted">ThesisDocument JSON 可交给后端执行 validate-input、render、OpenXML 验证和格式检查，再生成 DOCX。</p>
              </Card>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
