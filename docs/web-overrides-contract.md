# Web Editor — DocumentOverrides 契约

## 1. 目的

`DocumentOverrides` 是 web 编辑器引入的「文档级格式覆盖层」。它**不修改** `ThesisDocument` schema，也**不替代** `ThesisFormatSpec`／`TemplatePackage`，而是提供「在模板默认格式之上为本文档做局部偏离」的合法入口。

设计动机：

- `ThesisDocument` schema 把文档关进了固定结构（8 种 `SectionKind`、`additionalProperties: false`）—— 我们不打算从内容侧扩展。
- `ThesisFormatSpec` 由模板供给，是跨文档共享的格式规则 —— 不允许某一篇论文随手把模板改了。
- 但现实中作者经常需要「这篇用 1.25 倍行距」「这篇目录只到 H2」「这篇前置页用罗马数字页码」之类的偏离。

**结论**：用户的偏离作为 spec 的 **delta**，存在 envelope 层的 `overrides` 字段里。渲染时 `effectiveSpec = mergeSpec(template.formatSpec, document.overrides)` 再喂给 `DocxRenderer`。

这份文档定义这条契约，方便 schema worker 扩 schema、API worker 扩 DTO、renderer worker 实现 merge。

## 2. 数据模型（前端权威）

源文件：`web/src/editor/overrides.ts`

```ts
export interface DocumentOverrides {
  toc?: {
    minLevel?: number;        // 1..6
    maxLevel?: number;        // 1..6, ≥ minLevel
    title?: string;
    /**
     * 限定目录引用的章节集合（按 section.id 匹配）。
     *  - undefined：包含全部章节（默认）
     *  - 非空数组：仅包含其中的 id
     *  - 空数组：不引用任何章节（UI 应避免该状态）
     */
    includeSectionIds?: string[];
  };

  headerFooter?: {
    headerText?: string;
    drawHeaderLine?: boolean;
    hidePageNumberOnCover?: boolean;
    differentFirstPage?: boolean;
  };

  defaultFont?: FontOverride;
  bodyParagraph?: ParagraphOverride;

  /** 1..6 → 该级别标题的覆盖；省略级别 = 不覆盖 */
  headings?: Partial<Record<'1' | '2' | '3' | '4' | '5' | '6', HeadingOverride>>;

  /** 渲染器 3 桶（cover / frontMatter / body）的覆盖 */
  sectionFormats?: Partial<Record<SectionBucket, SectionFormatOverride>>;

  /**
   * 单个 Section 实例的覆盖（按 section.id 索引）。
   * 同一 section 同时有 sectionFormats[bucket] 和 sectionInstances[id] 时，
   * sectionInstances 优先。
   */
  sectionInstances?: Record<string, SectionInstanceOverride>;
}

export type SectionBucket = 'cover' | 'frontMatter' | 'body';

export type PageNumberStyle =
  | 'none' | 'decimal' | 'lowerRoman' | 'upperRoman';

export interface FontOverride {
  eastAsia?: string;
  latin?: string;
  sizePt?: number;     // 1..72
  bold?: boolean;
  italic?: boolean;
}

export type TextAlignment = 'left' | 'center' | 'right' | 'both';

export interface ParagraphOverride {
  lineSpacingMultiple?: number;     // 0.5..4
  spaceBeforePt?: number;           // 0..72
  spaceAfterPt?: number;            // 0..72
  firstLineIndentChars?: number;    // 0..8
  hangingIndentCm?: number;         // 0..5
  alignment?: TextAlignment;
  widowControl?: boolean;
}

export interface HeadingOverride {
  font?: FontOverride;
  spaceBeforePt?: number;
  spaceAfterPt?: number;
  numbered?: boolean;
  pageBreakBefore?: boolean;
  outlineLevel?: number;            // 0..8
  alignment?: TextAlignment;
}

export interface SectionFormatOverride {
  pageNumberStyle?: PageNumberStyle;
  startPageNumber?: number;         // 1..999
  restartPageNumbering?: boolean;
  includeHeader?: boolean;
  includeFooter?: boolean;
}

export interface SectionInstanceOverride extends SectionFormatOverride {
  /** 仅这一节使用的页眉文本 */
  headerText?: string;
  /** 仅这一节使用的页脚文本 */
  footerText?: string;
  /** 仅这一节内段落的覆盖 */
  paragraph?: ParagraphOverride;
  /** 仅这一节内默认字体的覆盖 */
  defaultFont?: FontOverride;
}
```

值的范围与 `ThesisFormatSpec` 中对应字段一致（参见 `schemas/thesis-format-spec.schema.json`）。前端目前用 `stripEmpty` 做空字段剥离，保证「未设置」严格等价于不存在。

## 3. SectionKind → SectionBucket 映射

渲染器历来把 8 种 `SectionKind` 折叠成 3 种 `SectionProfile`，这套映射是**契约的一部分**，前后端必须一致：

| SectionKind            | SectionBucket | SectionProfile (renderer)           |
|------------------------|---------------|-------------------------------------|
| `cover`                | `cover`       | `SectionProfile.Cover`              |
| `originalityStatement` | `frontMatter` | `SectionProfile.FrontMatter`        |
| `abstract`             | `frontMatter` | `SectionProfile.FrontMatter`        |
| `toc`                  | `frontMatter` | `SectionProfile.FrontMatter`        |
| `body`                 | `body`        | `SectionProfile.Body`               |
| `acknowledgements`     | `body`        | `SectionProfile.Body`               |
| `bibliography`         | `body`        | `SectionProfile.Body`               |
| `appendix`             | `body`        | `SectionProfile.Body`               |

源：`web/src/editor/overrides.ts:SECTION_BUCKET_FOR_KIND` 与 `src/ThesisDocx.Core/Rendering/SectionProfile.cs`。

## 4. 持久化层（当前阶段）

- **localStorage key**: `thesisforma.overrides.v1:{documentId}`
- **Value**: `JSON.stringify(DocumentOverrides)`，剥除空字段。
- **作用域**: 仅当前浏览器；**不进 API、不进文档 JSON**。这是当前 stage 的妥协，等下一节里描述的后端契约接通后会改。

## 5. 后端契约（目标）

### 5.1 API

`SaveDocumentRequest` / `ImportDocumentRequest` / `RenderRequest` 增加可选字段：

```json
{
  "document": {...},
  "templateId": "...",
  "overrides": { ... }
}
```

`DocumentEnvelope` DTO 同步增加：

```cs
public sealed record DocumentEnvelope(
    string Id,
    string? TemplateId,
    ThesisDocument Document,
    DocumentOverrides? Overrides,
    string UpdatedAt
);
```

JSON schema 建议为 `DocumentOverrides` 单独建一份 `schemas/document-overrides.schema.json`，由 schema worker 落地。结构与 §2 对齐，所有字段 `additionalProperties: false`。

### 5.2 Renderer 合并点

`DocxRenderContext` 新增 `Overrides` 属性。`DocumentPackageBuilder` / `DocxRenderer` 入口处把模板里的 `ThesisFormatSpec` 和文档的 `Overrides` 合并成 **effective spec**，然后照旧渲染：

```cs
var effectiveSpec = SpecMerger.Merge(template.FormatSpec, context.Overrides);
var sectionBuilder = new SectionBuilder(effectiveSpec, ...);
```

`SpecMerger.Merge(baseSpec, overrides)` 规则：

1. **字段级合并**：override 中存在的字段覆盖 baseSpec 同名字段；override 中不存在的字段保留 baseSpec。
2. **嵌套对象**：递归合并；override 不需提供完整对象。
3. **headings**：`overrides.headings['1']` 与 `baseSpec.headings[1]` 合并；其它级别保留 baseSpec。
4. **sectionFormats**：`overrides.sectionFormats[bucket]` 与 `baseSpec.sections[bucket]` 合并。
5. **toc.includeSectionIds**：渲染 `toc` 章节时，若该字段存在且非空，渲染器需要把目录限制在所列章节范围内。Word 的 `TOC` field 用 `\b <bookmark>` 限范围，`\b` 只支持单 bookmark，因此推荐两种实现路径：

   - **多字段串接**（推荐）：渲染器先在每个 `Section` 起止处发 `<w:bookmarkStart>` / `<w:bookmarkEnd>`（命名约定如 `_TocSec_<sectionId>`），目录章节内为每个被选中的 section emit 一段 `TOC \b _TocSec_<sectionId> \o "{min}-{max}" \h \z \u`，串行排版。优点：保留 Word 的 F9 自动更新。
   - **静态目录**：渲染器自己遍历选中 section 的 heading，按 `min`/`max` 过滤，直接 emit 文本 + `PAGEREF` 字段。优点：实现更直接；缺点：行间距/制表位需自己处理。

   `SECTION_BUCKET_FOR_KIND` 仍旧适用：被排除的章节本身仍正常渲染，只是不在目录里。

6. **sectionInstances**（新增能力）：在 `BodyRenderer` 输出每个 `Section` 时，如果该 `section.id` 在 `overrides.sectionInstances` 里：
   - SectionFormatOverride 字段（pageNumberStyle / restart / start / includeHeader / includeFooter）覆盖该 section 落地的 `SectionProperties`。
   - `headerText` / `footerText` 触发**逐节 header/footer part**：renderer 为这一节单独生成 `header{N}.xml` / `footer{N}.xml`，并在 sectPr 引用。这意味着 bucket 层面共享 header 的优化要让位，需要重新切分 `_headerFooterBuilder`。
   - `paragraph` / `defaultFont`：在该 section 范围内的段落 / run 上，作为附加 paragraphProperty / runProperty 直接体现。或者更干净地：为该 section 派生一组 `StyleId`，挂上覆盖样式，段落引用派生样式。后者更符合 §6 的「样式优先」。

合并的纯函数实现属于 renderer worker 的私域。建议附完整字段表的 fixture 测试。

### 5.3 验证

- API 层接到 `overrides` 后先按 `document-overrides.schema.json` 校验；不通过返回 400。
- `OpenXmlValidator` 不需要改 —— 合并产生的 effectiveSpec 仍然满足现有结构。
- `FormatConformanceValidator` 是否要把 effectiveSpec 也跑一遍要 schema/validation worker 评估。

## 6. 关于「单段落自定义」

用户反馈想给单个段落改字体／字号／首行缩进／行距。

**当前不直接实现**，原因：

1. **AGENTS.md 硬规则**："Web editor workers... must keep the editor structure-first... Do not add manual font, font-size, margin, line-spacing, or Word-like free layout controls."
2. 自由形式的逐段格式化会让模板失去意义，最终把这个产品退化成 Word。

**结构先行的替代方案**（推荐）：

- `ParagraphBlock.styleId` 字段 schema 已有，但目前没有 UI。后续在段落工具栏加「样式」下拉，用户从模板暴露的命名样式（如 `Body`、`Quotation`、`AbstractText`、`Acknowledgement`）里挑一个；模板那边把每个 styleId 映射到一组段落属性。结果是「段落选样式 → 模板决定怎么排」。
- 同理 run 级用 `TextInline` 的 mark（已有 `bold` / `italic` / `underline`）走结构。

**自由形式覆盖（非必要不做）**：

如果未来确实要暴露"这一段就是要单独 1.25 行距"，应在 `DocumentOverrides` 加：

```ts
blockOverrides?: Record<string /* block.id */, BlockFormatOverride>;

interface BlockFormatOverride {
  paragraph?: ParagraphOverride;
  defaultFont?: FontOverride;
}
```

理由：

- 用 block id 做键，不动 schema 的 `additionalProperties: false`。
- 把 free-form 控制集中在 `overrides`，避免散落到内容数据里。
- 哪怕开了这个口子，UI 也建议做成"高级抽屉"，默认不显眼，迫使用户先尝试 styleId。

落地前需要 schema worker / renderer worker 同意。

## 7. 前端实现位置（当前 stage）

| 关注点                | 文件                                                           |
|-----------------------|----------------------------------------------------------------|
| 类型与持久化          | `web/src/editor/overrides.ts`                                  |
| UI 面板               | `web/src/editor/panels/OverridesPanel.tsx` + `.module.css`      |
| 入口路由              | `EditorContext` 的 `view = { kind: 'overrides' }`              |
| 测试                  | `web/src/tests/overrides.test.ts`                              |

UI 已分组到：目录 / 全局页眉页脚 / 默认字体 / 正文段落 / 各级标题 / 章节分组 / 单节实例 / 重置。

## 8. 阶段性 TODO

| Owner               | 任务                                                                                  |
|---------------------|---------------------------------------------------------------------------------------|
| schema worker       | 起草 `schemas/document-overrides.schema.json`，加入示例与版本字段                     |
| api worker          | 扩 `DocumentEnvelope` 与 `SaveDocumentRequest`/`ImportDocumentRequest`，存读 round-trip |
| renderer worker     | 实现 `SpecMerger.Merge` 与 `BodyRenderer` 对 `sectionInstances` 的处理（含 per-section header/footer parts） |
| validation worker   | 决定 effectiveSpec 是否要再走 `FormatConformanceValidator`                            |
| web editor worker   | 接通 API 后，从 `OverridesPanel` 直接写入 envelope 而不是 localStorage；保留迁移窗口  |

## 9. 兼容与迁移

- 当前 localStorage key 用了 `v1:` 前缀；后续如果数据形态变化，新版本用 `v2:` 并提供一次性迁移函数（`migrateOverridesV1ToV2`）。
- 后端落地后，前端在打开文档时优先采用 envelope.overrides；本地仍有 v1 草稿就提示用户合并/丢弃。
