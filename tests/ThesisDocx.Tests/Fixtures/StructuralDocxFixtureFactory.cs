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
                            new TableCellNode { Text = "Merged", GridSpan = 2 },
                            new TableCellNode { Text = "Tail" }
                        ]
                    },
                    new TableRowNode
                    {
                        Cells =
                        [
                            new TableCellNode { Text = "VMerge", VerticalMerge = VerticalMergeKind.Restart },
                            new TableCellNode { Text = "A" },
                            new TableCellNode { Text = "B" }
                        ]
                    },
                    new TableRowNode
                    {
                        Cells =
                        [
                            new TableCellNode { Text = string.Empty, VerticalMerge = VerticalMergeKind.Continue },
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
