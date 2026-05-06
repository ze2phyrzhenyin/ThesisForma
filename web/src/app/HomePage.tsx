import { Badge, Button, Card, EmptyState, InlineAlert } from '../components/design-system/Primitives';

export function HomePage({ onNew, onTemplates }: { onNew: () => void; onTemplates: () => void }) {
  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <span className="brand-title">文范 ThesisForma</span>
          <span className="brand-subtitle">结构化录入论文内容，模板决定 DOCX 格式</span>
        </div>
      </header>
      <main className="page">
        <div className="home-shell">
          <section className="hero-panel">
            <div className="inline-row">
              <Badge tone="info">Frontend MVP</Badge>
              <Badge>结构化编辑</Badge>
            </div>
            <h1>论文结构化编辑器</h1>
            <p>录入论文结构内容，选择模板，导出可渲染的 ThesisDocument JSON。格式由模板统一控制，网页不提供手工字体、字号、行距和页边距设置。</p>
            <div className="inline-row hero-actions">
              <Button variant="primary" onClick={onNew}>新建论文</Button>
              <Button onClick={onTemplates}>选择模板</Button>
              <Button onClick={() => document.querySelector<HTMLInputElement>('[data-home-import]')?.click()}>导入 JSON</Button>
              <input data-home-import type="file" accept="application/json,.json" hidden aria-label="导入 JSON 草稿" />
            </div>
          </section>

          <InlineAlert title="Vercel 前端模式">
            当前线上版本支持结构化编辑、本地保存、导入 JSON 和导出 JSON；DOCX 生成需要连接后端 OpenXML 渲染服务。
          </InlineAlert>

          <div className="landing-grid">
            <Card title="最近草稿" description="本地草稿保存在浏览器中，不写入 examples 或公开仓库。">
              <EmptyState
                title="暂无最近草稿"
                description="点击“新建论文”，先填写题目、作者和正文结构。"
                action={<Button variant="primary" onClick={onNew}>开始新建</Button>}
              />
            </Card>
            <Card title="结构化工作方式" description="用户只负责内容和结构。">
              <p>封面、声明页、目录、页眉页脚、字体字号和行距由模板与后端渲染器控制，避免手工排版漂移。</p>
            </Card>
            <Card title="导出与后端渲染" description="前端先产出稳定 JSON。">
              <p>ThesisDocument JSON 可交给后端执行 validate-input、render、OpenXML 验证和格式检查，再生成 DOCX。</p>
            </Card>
          </div>
        </div>
      </main>
    </div>
  );
}
