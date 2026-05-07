import { useEffect, useState } from 'react';
import { templateApi } from '../api/client';
import { Badge, Button, EmptyState, StatusPill } from '../components/ui/Primitives';
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
    void templateApi
      .list()
      .then(setTemplates)
      .catch(() => setTemplates([]));
  }, []);

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark">ThesisForma</span>
          <span className="brand-sub">选择论文模板</span>
        </div>
        <div className="toolbar-spacer" />
        {onBack ? (
          <Button type="button" onClick={onBack}>
            返回首页
          </Button>
        ) : null}
      </header>

      <main className="page">
        <header className="page-header">
          <div>
            <span className="eyebrow">Template library</span>
            <h1>选择格式模板，但不要在网页里手工排版。</h1>
            <p>
              模板决定格式规则，编辑器只记录论文内容结构。公开前端只展示虚构示例模板；
              真实学院草稿应留在私有 workspace。
            </p>
          </div>
          <div className="alert muted" style={{ maxWidth: 280 }}>
            <span className="alert-title">前端模式</span>
            <span className="alert-body">
              可编辑结构、查看模板状态并导出 JSON。在线 DOCX 生成需连接后端服务。
            </span>
          </div>
        </header>

        <div className="template-grid">
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

function TemplateCard({
  template,
  onSelect
}: {
  template: TemplateSummary;
  onSelect: (id: string) => void;
}) {
  const isReady = template.status === 'ready';
  const coveragePct = Math.round(template.coverage * 100);
  const coverageVariant = coveragePct >= 90 ? '' : coveragePct >= 70 ? 'warning' : 'danger';
  const coverageTone = coveragePct >= 90 ? 'success' : coveragePct >= 70 ? 'warning' : 'danger';
  const knownGapsValue = isReady ? '0' : '待确认';

  return (
    <article className="template-card">
      <div className="row between" style={{ alignItems: 'flex-start' }}>
        <div>
          <h3>{template.name}</h3>
          <p className="school">
            {template.school} · {template.college}
          </p>
        </div>
        <StatusPill
          status={
            template.status === 'ready' ? 'ready' : template.status === 'notReady' ? 'notReady' : 'draft'
          }
        >
          {template.status}
        </StatusPill>
      </div>

      <div className="row">
        <Badge outline>版本 {template.version}</Badge>
        <Badge tone={coverageTone}>{coveragePct}% 覆盖</Badge>
      </div>

      <div
        className={['coverage-bar', coverageVariant].filter(Boolean).join(' ')}
        aria-label={`覆盖率 ${coveragePct}%`}
      >
        <span style={{ width: `${Math.min(100, Math.max(0, coveragePct))}%` }} />
      </div>

      <div className="template-detail">
        <div>
          <span className="lbl">Readiness</span>
          <span className="val">{template.readiness}</span>
        </div>
        <div>
          <span className="lbl">Known gaps</span>
          <span className="val">{knownGapsValue}</span>
        </div>
        <div>
          <span className="lbl">前端支持</span>
          <span className="val">结构化编辑 + JSON 导出</span>
        </div>
        <div>
          <span className="lbl">输出格式</span>
          <span className="val">DOCX (后端生成)</span>
        </div>
      </div>

      {!isReady ? (
        <p className="helper">此模板仍是草稿，可能存在未映射格式要求。</p>
      ) : null}

      <div className="template-card-foot">
        <span className="muted helper">id: {template.id}</span>
        <Button variant="primary" onClick={() => onSelect(template.id)}>
          选择模板
        </Button>
      </div>
    </article>
  );
}
