import { Button, Card, EmptyState } from '../components/design-system/Primitives';

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
        <section className="hero-panel">
          <h1>论文格式化结构编辑器</h1>
          <p>录入标题、段落、图表、脚注和参考文献，选择学院模板，导出合规 DOCX。这里不是 Word 替代品，也不提供手工字体字号调整。</p>
          <div className="toolbar-actions" style={{ marginTop: 18 }}>
            <Button variant="primary" onClick={onNew}>新建论文</Button>
            <Button onClick={onTemplates}>选择模板</Button>
            <Button>打开草稿</Button>
          </div>
        </section>
        <div className="landing-grid">
          <Card title="最近草稿">
            <EmptyState title="暂无最近草稿。点击“新建论文”开始录入结构化内容。" />
          </Card>
          <Card title="工作方式">
            <p>你只维护论文内容和结构。封面、声明页、目录、页眉页脚、字体字号和行距由模板与后端渲染器控制。</p>
          </Card>
          <Card title="导出链路">
            <p>保存 ThesisDocument JSON 后，API 会执行 validate-input、render、OpenXML 验证和格式检查，再提供 DOCX 下载。</p>
          </Card>
        </div>
      </main>
    </div>
  );
}
