import { useEffect, useState } from 'react';
import { templateApi } from '../api/client';
import { Badge, Button, EmptyState, InlineAlert, StatusPill } from '../components/design-system/Primitives';
import type { TemplateSummary } from '../components/thesis-editor/types';

export function TemplatesPage({
  onSelect,
  onBack
}: {
  onSelect: (templateId: string) => void;
  onBack?: () => void;
}) {
  const [templates, setTemplates] = useState<TemplateSummary[]>([]);

  useEffect(() => {
    void templateApi.list().then(setTemplates).catch(() => setTemplates([]));
  }, []);

  return (
    <div className="app-shell">
      <header className="topbar topbar-home">
        <div className="brand">
          <span className="brand-title">ThesisForma</span>
          <span className="brand-subtitle">选择论文模板</span>
        </div>
        {onBack ? (
          <nav className="toolbar-nav" aria-label="页面导航">
            <Button type="button" onClick={onBack}>返回首页</Button>
          </nav>
        ) : null}
      </header>
      <main className="page">
        <div className="home-shell">
          <section className="hero-panel">
            <h1>模板库</h1>
            <p>模板决定格式规则，编辑器只记录论文内容结构。公开前端只展示虚构示例模板；真实学院草稿应留在私有 workspace。</p>
          </section>
          <InlineAlert title="选择模板后仍可导出 JSON">
            当前 Vercel 前端模式不在线生成 DOCX。模板用于约束结构、提示缺口和标记导出目标。
          </InlineAlert>
        </div>
        <div className="template-grid template-grid-page">
          {templates.length === 0 ? (
            <EmptyState
              title="没有找到模板"
              description="前端模式会使用内置虚构示例模板；如果仍为空，请刷新页面。"
            />
          ) : null}
          {templates.map(template => (
            <TemplateCard key={template.id} template={template} onSelect={onSelect} />
          ))}
        </div>
      </main>
    </div>
  );
}

function TemplateCard({ template, onSelect }: { template: TemplateSummary; onSelect: (id: string) => void }) {
  const isReady = template.status === 'ready';
  const coveragePct = Math.round(template.coverage * 100);
  const coverageTone = coveragePct >= 90 ? 'success' : coveragePct >= 70 ? 'warning' : 'danger';
  const knownGapsValue = isReady ? 0 : '待确认';

  return (
    <article className="template-card">
      <div className="template-card-header">
        <div className="template-card-name-row">
          <strong className="template-card-name">{template.name}</strong>
          <StatusPill status={template.status === 'ready' ? 'ready' : template.status === 'notReady' ? 'notReady' : 'draft'}>
            {template.status}
          </StatusPill>
        </div>
        <p className="template-card-school">{template.school} · {template.college}</p>
      </div>

      <div className="template-card-body">
        <div className="template-meta-row">
          <Badge>版本 {template.version}</Badge>
          <Badge tone={coverageTone}>{coveragePct}% 覆盖</Badge>
          {!isReady ? <Badge tone="warning">草稿</Badge> : null}
        </div>
        <div className="template-detail-grid">
          <p className="muted template-detail-small">Readiness: {template.readiness}</p>
          <p className="muted template-detail-small">Known gaps: {knownGapsValue}</p>
          <p className="muted template-detail-small">前端支持：结构化编辑与 JSON 导出</p>
        </div>
        {!isReady ? (
          <p className="template-card-warning">此模板仍是草稿，可能存在未映射格式要求。</p>
        ) : null}
      </div>

      <div className="template-card-footer">
        <Button variant="primary" onClick={() => onSelect(template.id)}>选择模板</Button>
      </div>
    </article>
  );
}
