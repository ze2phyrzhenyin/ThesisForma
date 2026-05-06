import { useEffect, useState } from 'react';
import { templateApi } from '../api/client';
import { Badge, Button, Card, EmptyState } from '../components/design-system/Primitives';
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
        <section className="hero-panel">
          <h1>模板库</h1>
          <p>BFA 等真实学院模板只应在私有 workspace 或开发模式中显示；公开示例只使用虚构学校。</p>
        </section>
        <div className="template-grid">
          {templates.length === 0 ? <EmptyState title="没有找到模板。请确认 API 已启动。" /> : null}
          {templates.map(template => (
            <Card
              key={template.id}
              title={template.name}
              footer={<Button variant="primary" onClick={() => onSelect(template.id)}>使用此模板</Button>}
            >
              <div className="stack">
                <p>{template.school} / {template.college}</p>
                <div className="inline-row">
                  <Badge tone={template.status === 'ready' ? 'success' : 'warning'}>{template.status}</Badge>
                  <Badge>{template.version}</Badge>
                  <Badge>{Math.round(template.coverage * 100)}% coverage</Badge>
                </div>
                <p className="muted">Readiness: {template.readiness}</p>
              </div>
            </Card>
          ))}
        </div>
      </main>
    </div>
  );
}
