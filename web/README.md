# ThesisForma Web

结构化论文写作前端，搭配 `src/ThesisDocx.Api` 使用。

## 快速开始

```bash
# 1. 启动后端（在仓库根）
dotnet run --project src/ThesisDocx.Api --launch-profile http

# 2. 启动前端
cd web
cp .env.example .env   # 默认指向 http://localhost:5143
npm install
npm run dev            # http://localhost:5173
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
- **格式不暴露**：用户视角只有「我学校的模板」+ 少量覆盖项，不直接填 cm/pt。

## 架构

```
src/
├─ pages/            HomePage / TemplatesPage / EditorPage
├─ editor/
│   ├─ store.ts            Zustand + immer + zundo
│   ├─ EditorContext.tsx   Provider + hooks
│   ├─ InlineEditor.tsx    每块独立的 TipTap 实例（IME 友好）
│   ├─ inline-nodes.ts     citation / reference 自定义 node
│   ├─ inlines.ts          Inline[] ↔ ProseMirror JSON
│   ├─ blocks/             11 种块的可视化编辑组件
│   ├─ canvas/             SectionNav + Canvas
│   ├─ panels/             右栏：参考文献 / 校验 / 元数据 / 模板变量
│   ├─ ExportModal.tsx     校验 + 渲染 + 下载流程
│   ├─ useAutoSave.ts      防抖自动保存
│   └─ useShortcuts.ts     ⌘Z 撤销 / ⌘⇧Z 重做 / ⌘. 专注模式
├─ api/                fetcher + TanStack Query hooks
├─ types/              从 schemas/*.json 手写的 TS 类型
└─ design/             tokens.css + global.css（CSS Modules + 设计令牌）
```

## 已支持

- 元数据表单
- 11 种块全部可识别；可视化编辑：段落 / 标题 / 列表 / 引文 / 图 / 表 / 公式 / 分页 / 分节 / 参考文献占位
- 行内：粗体 / 斜体 / 下划线 / 超链接 / 文献引用（@ 触发）/ 交叉引用（数据级）
- 参考文献库（右栏）
- 模板选择 + 变量查看
- 校验（含 path 跳转）
- 渲染 → DOCX 下载
- JSON 导入 / 导出
- 自动保存（防抖 1.5s）
- 撤销 / 重做（最多 200 步）
- 专注模式（⌘.）

## 暂未支持（明确不做或后续）

- 脚注 / 尾注的可视化编辑（数据级保留）
- 表格单元格合并的可视化操作（schema 支持）
- DOCX 导入（CLI 已有，REST 未暴露）
- 模板包可视化编辑（封面布局 DSL）
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

复制 `vercel.json`，在 Vercel 项目里设置环境变量：

```
VITE_API_BASE_URL=<生产 API 地址>
```
