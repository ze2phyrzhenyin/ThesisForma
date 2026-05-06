import { useEffect, useState } from 'react';
import { templateApi } from '../api/client';
import { Badge, Button, EmptyState, StatusPill } from '../components/design-system/Primitives';
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
        <div className="template-page-shell">
          <section className="template-hero">
            <div>
              <span className="home-eyebrow-text">TEMPLATE LIBRARY</span>
              <h1>选择格式模板，但不要在网页里手工排版。</h1>
              <p>模板决定格式规则，编辑器只记录论文内容结构。公开前端只展示虚构示例模板；真实学院草稿应留在私有 workspace。</p>
            </div>
            <div className="template-hero-card">
              <strong>前端模式</strong>
              <span>可编辑结构、查看模板状态并导出 JSON。</span>
              <span>在线 DOCX 生成需连接后端服务。</span>
            </div>
          </section>
        </div>
        <div className="template-grid template-grid-page template-grid-polished">
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
    <article className="template-card template-card-polished">
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
        <div className="coverage-meter" aria-label={`覆盖率 ${coveragePct}%`}>
          <span style={{ width: `${Math.min(100, Math.max(0, coveragePct))}%` }} />
        </div>
        <div className="template-detail-grid">
          <div className="template-detail-item"><span className="template-detail-label">Readiness</span><span className="template-detail-value">{template.readiness}</span></div>
          <div className="template-detail-item"><span className="template-detail-label">Known gaps</span><span className="template-detail-value">{knownGapsValue}</span></div>
          <div className="template-detail-item"><span className="template-detail-label">前端支持</span><span className="template-detail-value">结构化编辑与 JSON 导出</span></div>
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
