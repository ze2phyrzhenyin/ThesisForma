using ThesisDocx.Core.Models;
using ThesisDocx.Core.Rendering;

namespace ThesisDocx.Tests.Fixtures;

internal static class StructuralDocxFixtureFactory
{
    public static string RenderNotesDocx()
    {
        var document = BaseDocument([
            new ParagraphBlock
            {
                Inlines =
                [
                    new TextInline { Text = "Text with notes" },
                    new FootnoteInline { NoteId = "fn-structural", Inlines = [new TextInline { Text = "Footnote structural text" }] },
                    new EndnoteInline { NoteId = "en-structural", Inlines = [new TextInline { Text = "Endnote structural text" }] }
                ]
            }
        ]);
        return TestRenderHelper.RenderDocument(document, new ThesisFormatSpec()).DocxPath;
    }

    public static string RenderAdvancedTableDocx()
    {
        var document = BaseDocument([
            new TableBlock
            {
                Caption = "Advanced table",
                Width = new TableWidthSpec { Type = TableWidthKind.Percent, Value = 85 },
                Layout = TableLayoutKind.Fixed,
                RepeatHeaderRows = 1,
                Borders = new TableBordersSpec
                {
                    Top = new BorderSpec { Style = BorderStyleKind.Single, Size = 8 },
                    Bottom = new BorderSpec { Style = BorderStyleKind.Single, Size = 8 },
                    Left = new BorderSpec { Style = BorderStyleKind.Single, Size = 4 },
                    Right = new BorderSpec { Style = BorderStyleKind.Single, Size = 4 },
                    InsideH = new BorderSpec { Style = BorderStyleKind.Single, Size = 4 },
                    InsideV = new BorderSpec { Style = BorderStyleKind.Single, Size = 4 }
                },
                Rows =
                [
                    new TableRowNode
                    {
                        IsHeader = true,
                        CantSplit = true,
                        Cells =
                        [
                            new TableCellNode { Text = "Merged", GridSpan = 2, VerticalAlignment = TableCellVerticalAlignment.Center },
                            new TableCellNode { Text = "Tail", VerticalAlignment = TableCellVerticalAlignment.Center }
                        ]
                    },
                    new TableRowNode
                    {
                        Cells =
                        [
                            new TableCellNode
                            {
                                Text = "VMerge",
                                VerticalMerge = VerticalMergeKind.Restart,
                                WidthCm = 3,
                                VerticalAlignment = TableCellVerticalAlignment.Center,
                                Borders = new TableBordersSpec { Bottom = new BorderSpec { Style = BorderStyleKind.Double, Size = 8 } }
                            },
                            new TableCellNode { Text = "A" },
                            new TableCellNode { Text = "B" }
                        ]
                    },
                    new TableRowNode
                    {
                        Cells =
                        [
                            new TableCellNode { Text = string.Empty, VerticalMerge = VerticalMergeKind.Continue, WidthCm = 3 },
                            new TableCellNode { Text = "C" },
                            new TableCellNode { Text = "D" }
                        ]
                    }
                ]
            }
        ]);
        return TestRenderHelper.RenderDocument(document, new ThesisFormatSpec()).DocxPath;
    }

    public static string RenderCustomPropertiesDocx()
    {
        return TestRenderHelper.RenderDocument(
            BaseDocument([new ParagraphBlock { Inlines = [new TextInline { Text = "Custom property carrier" }] }]),
            new ThesisFormatSpec(),
            new DocxRenderContext
            {
                TemplateId = "structural-template",
                TemplateVersion = "1.0.0",
                ResolvedFormatSpecVersion = "1.2.0"
            }).DocxPath;
    }

    private static ThesisDocument BaseDocument(List<BlockNode> blocks)
    {
        return new ThesisDocument
        {
            Metadata = new ThesisMetadata
            {
                Title = "Structural Fixture",
                Author = "Example Author",
                College = "Example College",
                Major = "Software Engineering",
                StudentId = "202600000001",
                Advisor = "Example Advisor",
                Date = "2026-05-08"
            },
            Sections =
            [
                new ThesisSection
                {
                    Kind = ThesisSectionKind.Body,
                    Blocks = blocks
                }
            ]
        };
    }
}
