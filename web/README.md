# ThesisForma Web

结构化论文写作前端。没有配置 `VITE_API_BASE_URL` 时，它作为静态本地编辑器运行：文档保存在浏览器 localStorage，支持 ThesisDocument / TemplatePackage JSON 导入导出，不依赖生产后端。

它不做 AI 解析，不做 DOCX 前端导入，不改写论文内容。DOCX 渲染仍属于 `ThesisDocument + ThesisFormatSpec/TemplatePackage -> DOCX` 的后端/Core 契约。

## 快速开始

```bash
cd web
npm install
npm run dev            # http://localhost:5173
```

如需联调本地 API：

```bash
dotnet run --project src/ThesisDocx.Api --launch-profile http
cd web
cp .env.example .env   # 指向 http://localhost:5143
npm run dev
```

## 命令

| 命令              | 用途                       |
| ----------------- | -------------------------- |
| `npm run dev`     | 开发服务器（含 API 代理）  |
| `npm run build`   | TypeScript + Vite 生产构建 |
| `npm run preview` | 预览生产构建               |
| `npm run typecheck` | 仅 TypeScript 检查       |
| `npm test`        | 单元测试                   |
| `npm run e2e`     | Playwright e2e             |

## 设计原则

- **结构即骨架**：8 种段（封面 / 摘要 / 目录 / 正文…）固定，左栏导航。
- **资源独立**：参考文献、图片是独立资源库，正文里通过引用占位符指向。
- **格式边界清晰**：论文编辑器只改结构和内容；cm/pt 等格式字段只出现在 TemplatePackage 结构化编辑器中。

## 架构

```
src/
├─ pages/            HomePage / TemplatesPage / EditorPage
├─ editor/
│   ├─ store.ts            Zustand + immer + zundo
│   ├─ EditorContext.tsx   Provider + hooks
│   ├─ InlineEditor.tsx    每块独立的 TipTap 实例（IME 友好）
│   ├─ documentContract.ts ThesisDocument 导入 / 清理 / 轻量校验
│   ├─ localDrafts.ts      本地草稿保存 / 打开 / 复制 / 删除
│   ├─ tableOps.ts         表格网格、合并、拆分纯函数
│   ├─ notes.ts            脚注 / 尾注收集与更新
│   ├─ inline-nodes.ts     citation / reference / note 自定义 node
│   ├─ inlines.ts          Inline[] ↔ ProseMirror JSON
│   ├─ blocks/             11 种块的可视化编辑组件
│   ├─ canvas/             SectionNav + Canvas
│   ├─ panels/             右栏：参考文献 / 脚注尾注 / 校验 / 元数据 / 模板变量
│   ├─ ExportModal.tsx     校验 + 渲染 + 下载流程
│   ├─ useAutoSave.ts      防抖自动保存
│   └─ useShortcuts.ts     ⌘S 保存 / ⌘E 导出 / ⌘Z 撤销 / ⌘. 专注
├─ templates/         TemplatePackage 导入 / 清理 / Page Template helpers / 轻量校验
├─ api/                fetcher + TanStack Query hooks
├─ types/              从 schemas/*.json 手写的 TS 类型
└─ design/             tokens.css + global.css（CSS Modules + 设计令牌）
```

## 已支持

- 元数据表单
- 11 种块全部可识别；可视化编辑：段落 / 标题 / 列表 / 引文 / 图 / 表 / 公式 / 分页 / 分节 / 参考文献占位
- 行内：粗体 / 斜体 / 下划线 / 超链接 / 文献引用（@ 触发）/ 交叉引用 / 脚注 / 尾注
- 参考文献库（右栏）
- 脚注 / 尾注管理器：新增、编辑、删除、空内容和重复 id 校验
- 表格结构编辑：行列增删、表头行、cantSplit、横向/纵向/矩形合并、拆分已合并单元格，并有纯函数结构校验
- 本地草稿：新建、打开、复制、删除、刷新后恢复最近草稿
- TemplatePackage 编辑器：导入/导出模板 JSON、编辑基本信息、变量、assets、嵌入式 FormatSpec 基础字段、Page Template 元素
- Page Template 元素表单：spacer / text / metadataField / image / fieldTable / declarationText / pageBreak，含变量、metadata 和 asset 引用校验
- 模板选择 + 变量查看
- 校验（含 path 跳转）
- JSON 导入 / 导出
- 自动保存（防抖 1.5s）
- 撤销 / 重做（最多 200 步）
- 快捷键：⌘/Ctrl+S 保存、⌘/Ctrl+E 导出 JSON、⌘/Ctrl+. 专注模式

## 暂未支持（明确不做或后续）

- 静态前端不渲染 DOCX；DOCX 下载需要显式配置并运行 API
- DOCX 前端导入不做
- AI 解析 / AI 改写不做
- TemplatePackage 编辑器不导入目录或 zip，只导入核心 JSON 文件
- Page Template 只做安全的 schema 表单和结构化预览，不做 Word 式任意版式设计器
- 完整格式覆盖（需后端协作）

## 构建依赖

| 包                | 作用                      |
| ----------------- | ------------------------- |
| Vite + React 19   | 构建 + 渲染               |
| TypeScript        | 类型                      |
| TipTap (per-block) | 行内富文本（含 IME）     |
| Zustand + immer + zundo | 状态 + 撤销           |
| TanStack Query    | API 缓存 / 变更           |
| KaTeX             | 公式预览                  |
| Vitest + RTL      | 测试                      |

## 部署

Vercel 生产站点当前可以作为静态前端部署，不需要生产 API 环境变量。设置 `VITE_API_BASE_URL` 后，前端会使用 API 进行文档保存、校验和 DOCX 渲染联动。
