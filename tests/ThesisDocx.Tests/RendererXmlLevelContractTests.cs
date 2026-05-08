using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class RendererXmlLevelContractTests
{
    [Fact]
    public void RenderAdvancedTable_ShouldEmitMergePaginationWidthAndBorderXml()
    {
        var rendered = TestRenderHelper.RenderDocument(CreateTableDocument(), CreateFormat());
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var table = Assert.Single(package.MainDocumentPart!.Document.Descendants<W.Table>());
        var properties = table.GetFirstChild<W.TableProperties>()!;

        Assert.Equal(W.TableLayoutValues.Fixed, properties.GetFirstChild<W.TableLayout>()!.Type!.Value);
        Assert.Equal(W.TableWidthUnitValues.Pct, properties.GetFirstChild<W.TableWidth>()!.Type!.Value);
        Assert.Equal("4250", properties.GetFirstChild<W.TableWidth>()!.Width!.Value);
        Assert.NotNull(properties.GetFirstChild<W.TableCellMarginDefault>());

        Assert.Contains(table.Descendants<W.GridSpan>(), span => span.Val?.Value == 2);
        Assert.Contains(table.Descendants<W.VerticalMerge>(), merge => merge.Val?.Value == W.MergedCellValues.Restart);
        Assert.Contains(table.Descendants<W.VerticalMerge>(), merge => merge.Val?.Value == W.MergedCellValues.Continue);
        Assert.Contains(table.Descendants<W.TableHeader>(), _ => true);
        Assert.Contains(table.Descendants<W.CantSplit>(), _ => true);
        Assert.Contains(table.Descendants<W.TableCellWidth>(), width => width.Type?.Value == W.TableWidthUnitValues.Dxa && width.Width?.Value == UnitConverter.CentimetersToTwips(3).ToString());
        Assert.Contains(table.Descendants<W.TableCellVerticalAlignment>(), alignment => alignment.Val?.Value == W.TableVerticalAlignmentValues.Center);
        Assert.Contains(table.Descendants<W.TableCellBorders>(), borders => borders.BottomBorder?.Val?.Value == W.BorderValues.Double);
    }

    [Fact]
    public void RenderNotes_ShouldEmitBodyReferencesPartsSeparatorsAndContent()
    {
        var document = CreateBaseDocument([
            new ParagraphBlock
            {
                Inlines =
                [
                    new TextInline { Text = "正文脚注" },
                    new FootnoteInline { NoteId = "fn-1", Inlines = [new TextInline { Text = "脚注内容" }] },
                    new TextInline { Text = "和尾注" },
                    new EndnoteInline { NoteId = "en-1", Inlines = [new TextInline { Text = "尾注内容" }] }
                ]
            }
        ]);
        var rendered = TestRenderHelper.RenderDocument(document, CreateFormat());
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        Assert.Contains(package.MainDocumentPart!.Document.Descendants<W.FootnoteReference>(), reference => reference.Id?.Value == 1);
        Assert.Contains(package.MainDocumentPart.Document.Descendants<W.EndnoteReference>(), reference => reference.Id?.Value == 1);

        var footnotes = package.MainDocumentPart.FootnotesPart!.Footnotes!;
        var endnotes = package.MainDocumentPart.EndnotesPart!.Endnotes!;
        Assert.Contains(footnotes.Elements<W.Footnote>(), note => note.Type?.Value == W.FootnoteEndnoteValues.Separator && note.Id?.Value == -1);
        Assert.Contains(footnotes.Elements<W.Footnote>(), note => note.Type?.Value == W.FootnoteEndnoteValues.ContinuationSeparator && note.Id?.Value == 0);
        Assert.Contains(endnotes.Elements<W.Endnote>(), note => note.Type?.Value == W.FootnoteEndnoteValues.Separator && note.Id?.Value == -1);
        Assert.Contains(endnotes.Elements<W.Endnote>(), note => note.Type?.Value == W.FootnoteEndnoteValues.ContinuationSeparator && note.Id?.Value == 0);
        Assert.Contains(footnotes.Descendants<W.Text>(), text => text.Text == "脚注内容");
        Assert.Contains(endnotes.Descendants<W.Text>(), text => text.Text == "尾注内容");
    }

    [Fact]
    public void RenderEquationBookmarkAndRef_ShouldEmitOmmlNumberBookmarkAndField()
    {
        var document = CreateBaseDocument([
            new HeadingBlock { Id = "h1", Level = 1, Inlines = [new TextInline { Text = "第一章" }] },
            new EquationBlock
            {
                BookmarkName = "bm-eq-contract",
                SourceType = EquationSourceType.Plain,
                PlainText = "E=mc^2",
                Numbering = new EquationNumberingSpec { Enabled = true, Format = "({chapter}.{index})", RestartByHeadingLevel = 1 }
            },
            new ParagraphBlock { Inlines = [new TextInline { Text = "见" }, new ReferenceInline { BookmarkName = "bm-eq-contract" }] }
        ]);
        var rendered = TestRenderHelper.RenderDocument(document, CreateFormat());
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        Assert.Contains(package.MainDocumentPart!.Document.Descendants<M.OfficeMath>(), math => math.Descendants<M.Text>().Any(text => text.Text.Contains("E=mc", StringComparison.Ordinal)));
        var bookmark = Assert.Single(package.MainDocumentPart.Document.Descendants<W.BookmarkStart>(), start => start.Name?.Value == "bm-eq-contract");
        Assert.Contains(package.MainDocumentPart.Document.Descendants<W.BookmarkEnd>(), end => end.Id?.Value == bookmark.Id?.Value);
        Assert.Contains(package.MainDocumentPart.Document.Descendants<W.SimpleField>(), field => field.Instruction?.Value?.Contains("REF bm-eq-contract", StringComparison.Ordinal) == true);
        Assert.Contains(package.MainDocumentPart.Document.Descendants<W.Text>(), text => text.Text.Contains("(1.1)", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderPageNumberingSections_ShouldEmitSectionPropertiesFooterReferencesAndPageFields()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var sections = package.MainDocumentPart!.Document.Body!.Descendants<W.SectionProperties>().ToList();
        Assert.Equal(3, sections.Count);
        Assert.Null(sections[0].GetFirstChild<W.PageNumberType>());
        Assert.Equal(W.NumberFormatValues.LowerRoman, sections[1].GetFirstChild<W.PageNumberType>()!.Format!.Value);
        Assert.Equal(1, sections[1].GetFirstChild<W.PageNumberType>()!.Start!.Value);
        Assert.Equal(W.NumberFormatValues.Decimal, sections[2].GetFirstChild<W.PageNumberType>()!.Format!.Value);
        Assert.Equal(1, sections[2].GetFirstChild<W.PageNumberType>()!.Start!.Value);
        Assert.True(sections.Skip(1).All(section => section.GetFirstChild<W.FooterReference>() is not null));
        Assert.Contains(package.MainDocumentPart.FooterParts.SelectMany(part => part.Footer.Descendants<W.SimpleField>()),
            field => field.Instruction?.Value?.Trim().StartsWith("PAGE", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void RenderCaptionsAndBibliography_ShouldEmitStylesNumberingAndHangingIndent()
    {
        var rendered = TestRenderHelper.RenderSimpleThesis();
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var captionParagraphs = package.MainDocumentPart!.Document.Descendants<W.Paragraph>()
            .Where(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value == ThesisDocx.Core.OpenXml.StyleIds.Caption)
            .ToList();
        Assert.Contains(captionParagraphs, paragraph => TextOf(paragraph).Contains("图1", StringComparison.Ordinal));
        Assert.Contains(captionParagraphs, paragraph => TextOf(paragraph).Contains("表1", StringComparison.Ordinal));

        var bibliographyParagraphs = package.MainDocumentPart.Document.Descendants<W.Paragraph>()
            .Where(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value == ThesisDocx.Core.OpenXml.StyleIds.Bibliography)
            .ToList();
        Assert.NotEmpty(bibliographyParagraphs);
        Assert.All(bibliographyParagraphs, paragraph =>
        {
            Assert.Equal(NumberingBuilder.BibliographyNumberingId, paragraph.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value);
            Assert.Equal(UnitConverter.CentimetersToTwips(0.74).ToString(), paragraph.ParagraphProperties.Indentation!.Hanging!.Value);
        });
        Assert.NotNull(package.MainDocumentPart.NumberingDefinitionsPart!.Numbering);
    }

    [Fact]
    public void RenderTemplatePackage_ShouldApplyTemplateMetadataWithoutLeakingLocalPathsOrFontBinaries()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var documentPath = Path.Combine(root, "examples", "full-thesis", "document.json");
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!;
        ResolveFigurePaths(document, Path.GetDirectoryName(documentPath)!);
        var resolution = new TemplateResolver().Resolve(Path.Combine(root, "examples", "templates", "example-university-engineering"), document, new Dictionary<string, string> { ["variables.confidentiality"] = "内部" });
        Assert.True(resolution.IsValid, string.Join(Environment.NewLine, resolution.Errors));
        var context = new DocxRenderContext
        {
            TemplateId = resolution.Template!.Id,
            TemplateVersion = resolution.Template.Version,
            TemplateSchool = resolution.Template.School,
            TemplateCollege = resolution.Template.College,
            ResolvedFormatSpecVersion = resolution.FormatSpec!.SchemaVersion,
            PageTemplates = resolution.PageTemplates,
            Variables = resolution.Variables.Where(variable => variable.Value is not null).ToDictionary(variable => variable.Name, variable => variable.Value!, StringComparer.Ordinal),
            Assets = resolution.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal)
        };

        var rendered = TestRenderHelper.RenderDocument(document, resolution.FormatSpec!, context);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var bodyText = TextOf(package.MainDocumentPart!.Document.Body!);
        Assert.Contains("Example University", bodyText, StringComparison.Ordinal);
        Assert.Contains("原创性声明", bodyText, StringComparison.Ordinal);
        Assert.Contains("内部", bodyText, StringComparison.Ordinal);
        Assert.Contains(package.CustomFilePropertiesPart!.Properties!.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>(),
            property => property.Name?.Value == "ThesisDocx.TemplateId");
        Assert.DoesNotContain(AllParts(package), part => part.Uri.OriginalString.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || part.Uri.OriginalString.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(root, ReadXmlLikePackageText(package), StringComparison.Ordinal);
    }

    private static ThesisDocument CreateTableDocument()
    {
        var table = new TableBlock
        {
            Caption = "复杂表格",
            BookmarkId = "bm-table-contract",
            Style = TableStyleKind.Custom,
            Width = new TableWidthSpec { Type = TableWidthKind.Percent, Value = 85 },
            Layout = TableLayoutKind.Fixed,
            RepeatHeaderRows = 1,
            AllowRowBreakAcrossPages = false,
            CellMargins = new TableCellMarginsSpec { TopCm = 0.05, BottomCm = 0.05, LeftCm = 0.08, RightCm = 0.08 },
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
                        new TableCellNode { Text = "合并表头", GridSpan = 2, Shading = "D9EAF7", VerticalAlignment = TableCellVerticalAlignment.Center },
                        new TableCellNode { Text = "列三", VerticalAlignment = TableCellVerticalAlignment.Center }
                    ]
                },
                new TableRowNode
                {
                    CantSplit = true,
                    Cells =
                    [
                        new TableCellNode
                        {
                            Text = "矩形合并",
                            GridSpan = 2,
                            VerticalMerge = VerticalMergeKind.Restart,
                            WidthCm = 3,
                            VerticalAlignment = TableCellVerticalAlignment.Center,
                            Borders = new TableBordersSpec { Bottom = new BorderSpec { Style = BorderStyleKind.Double, Size = 8 } }
                        },
                        new TableCellNode { Text = "C1" }
                    ]
                },
                new TableRowNode
                {
                    Cells =
                    [
                        new TableCellNode { Text = "", GridSpan = 2, VerticalMerge = VerticalMergeKind.Continue, WidthCm = 3 },
                        new TableCellNode { Text = "C2" }
                    ]
                }
            ]
        };

        return CreateBaseDocument([table]);
    }

    private static ThesisDocument CreateBaseDocument(List<BlockNode> blocks)
    {
        return new ThesisDocument
        {
            Metadata = new ThesisMetadata
            {
                Title = "XML Contract Thesis",
                Author = "测试作者",
                College = "示例学院",
                Major = "软件工程",
                StudentId = "20260001",
                Advisor = "导师",
                Date = "2026-05-08"
            },
            Sections =
            [
                new ThesisSection
                {
                    Id = "body",
                    Kind = ThesisSectionKind.Body,
                    Blocks = blocks
                }
            ]
        };
    }

    private static ThesisFormatSpec CreateFormat()
    {
        return new ThesisFormatSpec
        {
            HeaderFooter = new HeaderFooterFormatSpec { HeaderText = "XML Contract", PageNumberAlignment = TextAlignment.Center },
            Tables = new TableFormatSpec
            {
                DefaultLayout = TableLayoutKind.Fixed,
                DefaultWidth = new TableWidthSpec { Type = TableWidthKind.Percent, Value = 100 },
                AllowRowBreakAcrossPagesDefault = true,
                RepeatHeaderRowsDefault = 0
            }
        };
    }

    private static void AssertNoOpenXmlErrors(string docxPath)
    {
        var validation = new OpenXmlPackageValidator().Validate(docxPath);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
    }

    private static string TextOf(OpenXmlElement element)
    {
        return string.Concat(element.Descendants<W.Text>().Select(text => text.Text));
    }

    private static void ResolveFigurePaths(ThesisDocument document, string baseDirectory)
    {
        foreach (var figure in document.Sections.SelectMany(section => section.Blocks).OfType<FigureBlock>())
        {
            if (!string.IsNullOrWhiteSpace(figure.ImagePath) && !Path.IsPathRooted(figure.ImagePath))
            {
                figure.ImagePath = Path.GetFullPath(Path.Combine(baseDirectory, figure.ImagePath));
            }
        }
    }

    private static string ReadXmlLikePackageText(WordprocessingDocument document)
    {
        var texts = new List<string>();
        foreach (var part in AllParts(document).Where(IsTextLikePart))
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream);
            texts.Add(reader.ReadToEnd());
        }

        return string.Join(Environment.NewLine, texts);
    }

    private static IEnumerable<OpenXmlPart> AllParts(WordprocessingDocument document)
    {
        var roots = new List<OpenXmlPart>();
        if (document.MainDocumentPart is not null)
        {
            roots.Add(document.MainDocumentPart);
        }

        if (document.CustomFilePropertiesPart is not null)
        {
            roots.Add(document.CustomFilePropertiesPart);
        }

        if (document.CoreFilePropertiesPart is not null)
        {
            roots.Add(document.CoreFilePropertiesPart);
        }

        if (document.ExtendedFilePropertiesPart is not null)
        {
            roots.Add(document.ExtendedFilePropertiesPart);
        }

        foreach (var part in roots)
        {
            yield return part;
            foreach (var child in DescendantParts(part))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<OpenXmlPart> DescendantParts(OpenXmlPartContainer container)
    {
        foreach (var pair in container.Parts)
        {
            yield return pair.OpenXmlPart;
            foreach (var child in DescendantParts(pair.OpenXmlPart))
            {
                yield return child;
            }
        }
    }

    private static bool IsTextLikePart(OpenXmlPart part)
    {
        var uri = part.Uri.OriginalString;
        return uri.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || uri.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
            || uri.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }
}
