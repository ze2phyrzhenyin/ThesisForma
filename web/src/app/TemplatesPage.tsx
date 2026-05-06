import { useEffect, useState } from 'react';
import { templateApi } from '../api/client';
import { Badge, Button, Card, EmptyState, InlineAlert, StatusPill } from '../components/design-system/Primitives';
import type { TemplateSummary } from '../components/thesis-editor/types';

export function TemplatesPage({ onSelect }: { onSelect: (templateId: string) => void }) {
  const [templates, setTemplates] = useState<TemplateSummary[]>([]);

  useEffect(() => {
    void templateApi.list().then(setTemplates).catch(() => setTemplates([]));
  }, []);

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <span className="brand-title">选择论文模板</span>
          <span className="brand-subtitle">模板决定格式规则，编辑器只录入内容结构</span>
        </div>
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
          {templates.length === 0 ? <EmptyState title="没有找到模板" description="前端模式会使用内置虚构示例模板；如果仍为空，请刷新页面。" /> : null}
          {templates.map(template => (
            <Card
              key={template.id}
              title={template.name}
              description={`${template.school} / ${template.college}`}
              footer={
                <div className="inline-row template-card-footer">
                  <Button variant="primary" onClick={() => onSelect(template.id)}>选择模板</Button>
                  <Button variant="ghost">查看详情</Button>
                </div>
              }
            >
              <div className="template-card-meta">
                <div className="inline-row">
                  <StatusPill status={template.status === 'ready' ? 'ready' : template.status === 'notReady' ? 'notReady' : 'draft'}>{template.status}</StatusPill>
                  <Badge>{template.version}</Badge>
                  <Badge tone="info">{Math.round(template.coverage * 100)}% 覆盖</Badge>
                </div>
                <p className="muted">Readiness: {template.readiness}</p>
                <p className="muted">Known gaps: {template.status === 'ready' ? 0 : '待确认'}</p>
                <p>支持当前前端模式：可结构化编辑与导出 JSON。</p>
              </div>
            </Card>
          ))}
        </div>
      </main>
    </div>
  );
}
