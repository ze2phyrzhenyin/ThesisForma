using ThesisDocx.Core.Diff.Layout;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class LargeDocumentRobustnessTests
{
    private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [Fact]
    public void Renderer_ShouldHandleGeneratedLargeDocument()
    {
        var document = CreateLargeDocument();
        var format = new ThesisFormatSpec { Name = "large-document-robustness" };

        var rendered = TestRenderHelper.RenderDocument(document, format);
        var validation = new OpenXmlPackageValidator().Validate(rendered.DocxPath);
        var inspect = new DocxInspector().Inspect(rendered.DocxPath);
        var signature = new DocxLayoutSignatureExtractor().Extract(rendered.DocxPath);

        Assert.Empty(validation.Errors);
        Assert.True(inspect.ParagraphCount >= 1_300);
        Assert.True(inspect.TableCount >= 20);
        Assert.True(inspect.FiguresCount >= 20);
        Assert.True(signature.Tables.Count >= 20);
        Assert.True(signature.Equations.OmmlCount >= 20);
        Assert.True(signature.Footnotes.Count >= 50);
        Assert.True(new FileInfo(rendered.DocxPath).Length < 25 * 1024 * 1024);
    }

    [Fact]
    public void SnapshotNormalizer_ShouldBeStableForGeneratedLargeDocument()
    {
        var document = CreateLargeDocument();
        var format = new ThesisFormatSpec { Name = "large-document-robustness" };

        var first = TestRenderHelper.RenderDocument(document, format);
        var second = TestRenderHelper.RenderDocument(document, format);
        var normalizer = new DocxSnapshotNormalizer();

        Assert.Equal(
            normalizer.NormalizeToStableSnapshot(first.DocxPath),
            normalizer.NormalizeToStableSnapshot(second.DocxPath));
    }

    private static ThesisDocument CreateLargeDocument()
    {
        var document = new ThesisDocument
        {
            Metadata = new ThesisMetadata
            {
                Title = "Generated Large Thesis",
                Author = "Example Author",
                College = "Example Engineering College",
                Major = "Software Engineering",
                StudentId = "202600000001",
                Advisor = "Example Advisor",
                Date = "2026-05-08"
            }
        };

        for (var sectionIndex = 0; sectionIndex < 50; sectionIndex++)
        {
            var section = new ThesisSection
            {
                Id = $"sec-{sectionIndex:00}",
                Kind = sectionIndex == 49 ? ThesisSectionKind.Bibliography : ThesisSectionKind.Body,
                Title = $"Section {sectionIndex + 1}",
                StartOnNewPage = sectionIndex > 0
            };

            section.Blocks.Add(Heading(1, $"第 {sectionIndex + 1} 章 生成式章节", $"sec{sectionIndex:00}"));
            for (var headingIndex = 0; headingIndex < 5; headingIndex++)
            {
                section.Blocks.Add(Heading(headingIndex % 2 == 0 ? 2 : 3, $"{sectionIndex + 1}.{headingIndex + 1} 稳定性主题", $"sec{sectionIndex:00}h{headingIndex:00}"));
            }

            for (var paragraphIndex = 0; paragraphIndex < 20; paragraphIndex++)
            {
                section.Blocks.Add(Paragraph(sectionIndex, paragraphIndex));
            }

            if (sectionIndex < 20)
            {
                section.Blocks.Add(Table(sectionIndex));
                section.Blocks.Add(Figure(sectionIndex));
                section.Blocks.Add(Equation(sectionIndex));
            }

            if (sectionIndex == 49)
            {
                section.Blocks.Add(new BibliographyBlock
                {
                    Entries = Enumerable.Range(1, 40)
                        .Select(i => new BibliographyEntryNode { Id = $"bib-{i:00}", Text = $"[{i}] Example Author. Deterministic Rendering Study {i}. Example Press, 2026." })
                        .ToList()
                });
            }

            document.Sections.Add(section);
        }

        return document;
    }

    private static HeadingBlock Heading(int level, string text, string bookmarkName)
    {
        return new HeadingBlock
        {
            Level = level,
            BookmarkName = bookmarkName,
            Inlines = [Text(text)]
        };
    }

    private static ParagraphBlock Paragraph(int sectionIndex, int paragraphIndex)
    {
        var inlines = new List<InlineNode>
        {
            Text($"这是第 {sectionIndex + 1} 章第 {paragraphIndex + 1} 段的生成式正文，用于覆盖真实论文规模下的确定性渲染路径。"),
            new ReferenceInline { BookmarkName = $"sec{sectionIndex:00}", FallbackText = $"第 {sectionIndex + 1} 章" }
        };

        if (paragraphIndex == 3)
        {
            inlines.Add(new FootnoteInline { NoteId = $"fn-{sectionIndex:00}", Inlines = [Text($"第 {sectionIndex + 1} 章脚注内容。")] });
        }

        if (paragraphIndex == 7)
        {
            inlines.Add(new EndnoteInline { NoteId = $"en-{sectionIndex:00}", Inlines = [Text($"第 {sectionIndex + 1} 章尾注内容。")] });
        }

        return new ParagraphBlock { Inlines = inlines };
    }

    private static TableBlock Table(int index)
    {
        return new TableBlock
        {
            Caption = $"表 {index + 1} 生成数据表",
            Layout = TableLayoutKind.Fixed,
            RepeatHeaderRows = 1,
            Rows =
            [
                new TableRowNode
                {
                    IsHeader = true,
                    CantSplit = true,
                    Cells =
                    [
                        new TableCellNode { Text = "指标", GridSpan = 2, Shading = "D9EAF7", Alignment = TextAlignment.Center },
                        new TableCellNode { Text = "结论", Shading = "D9EAF7", Alignment = TextAlignment.Center }
                    ]
                },
                new TableRowNode
                {
                    Cells =
                    [
                        new TableCellNode { Text = "稳定性", VerticalMerge = VerticalMergeKind.Restart, VerticalAlignment = TableCellVerticalAlignment.Center },
                        new TableCellNode { Text = "通过" },
                        new TableCellNode { Text = "OpenXML clean" }
                    ]
                },
                new TableRowNode
                {
                    Cells =
                    [
                        new TableCellNode { Text = string.Empty, VerticalMerge = VerticalMergeKind.Continue },
                        new TableCellNode { Text = "规模" },
                        new TableCellNode { Text = "generated fixture" }
                    ]
                }
            ]
        };
    }

    private static FigureBlock Figure(int index)
    {
        return new FigureBlock
        {
            Caption = $"图 {index + 1} 生成占位图",
            ImageDataBase64 = TinyPngBase64,
            ImageContentType = "image/png",
            WidthCm = 2,
            HeightCm = 2
        };
    }

    private static EquationBlock Equation(int index)
    {
        return new EquationBlock
        {
            Placeholder = $"E = mc^2 ({index + 1})",
            SourceType = EquationSourceType.Plain,
            PlainText = $"E = mc^2 + {index}",
            BookmarkName = $"eq{index:00}",
            Caption = $"公式 {index + 1}",
            Numbering = new EquationNumberingSpec { Enabled = true }
        };
    }

    private static TextInline Text(string text) => new() { Text = text };
}
