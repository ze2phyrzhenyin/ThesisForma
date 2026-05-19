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
    public void RenderTableCellBlocks_ShouldEmitRichParagraphsNotesAndDirectFormatting()
    {
        var rendered = TestRenderHelper.RenderDocument(CreateRichCellTableDocument(), CreateFormat());
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var table = Assert.Single(package.MainDocumentPart!.Document.Descendants<W.Table>());
        var cell = Assert.Single(table.Descendants<W.TableCell>());
        var paragraphs = cell.Elements<W.Paragraph>().ToList();

        Assert.True(paragraphs.Count >= 4);
        Assert.Contains(paragraphs, paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value == ThesisDocx.Core.OpenXml.StyleIds.Heading2);
        Assert.Contains(cell.Descendants<W.Hyperlink>(), hyperlink => hyperlink.InnerText == "链接");
        Assert.Contains(cell.Descendants<W.FootnoteReference>(), reference => reference.Id?.Value == 1);
        Assert.Contains(cell.Descendants<W.Text>(), text => text.Text.Contains("•", StringComparison.Ordinal));
        Assert.Contains(cell.Descendants<W.FontSize>(), size => size.Val?.Value == UnitConverter.PointsToHalfPoints(9).ToString());
        Assert.Contains(paragraphs, paragraph => paragraph.ParagraphProperties?.SpacingBetweenLines?.Line?.Value == "240");
        Assert.Contains(package.MainDocumentPart.FootnotesPart!.Footnotes!.Descendants<W.Text>(), text => text.Text == "单元格脚注");

        var inspect = new DocxInspector().Inspect(rendered.DocxPath);
        Assert.True(inspect.Tables.HasNestedCellBlocks);
        Assert.True(inspect.Tables.HasCellNoteReferences);
        Assert.Contains(ThesisDocx.Core.OpenXml.StyleIds.Heading2, inspect.Tables.CellParagraphStyleIds);
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
    public void RenderExactLineSpacing_ShouldEmitExactSpacingInBodyStyle()
    {
        var format = CreateFormat();
        format.BodyParagraph.LineSpacingExactPt = 20;
        format.Sections = new Dictionary<string, SectionFormatSpec>
        {
            ["body"] = new() { PageNumberStyle = PageNumberStyle.Decimal, RestartPageNumbering = true }
        };
        var rendered = TestRenderHelper.RenderDocument(
            CreateBaseDocument([new ParagraphBlock { Inlines = [new TextInline { Text = "固定行距" }] }]),
            format);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var bodyStyle = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>()
            .Single(style => style.StyleId?.Value == ThesisDocx.Core.OpenXml.StyleIds.ThesisBody);
        var spacing = bodyStyle.GetFirstChild<W.StyleParagraphProperties>()!.GetFirstChild<W.SpacingBetweenLines>()!;

        Assert.Equal("400", spacing.Line?.Value);
        Assert.Equal(W.LineSpacingRuleValues.Exact, spacing.LineRule?.Value);
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

    [Fact]
    public void RenderPageTemplateRule_ShouldEmitParagraphBorderAndInspectionEvidence()
    {
        var document = new ThesisDocument
        {
            Metadata = new ThesisMetadata
            {
                Title = "Rule Template",
                Author = "测试作者",
                College = "示例学院",
                Major = "软件工程",
                StudentId = "20260001",
                Advisor = "导师",
                Date = "2026-05-12"
            },
            Sections =
            [
                new ThesisSection { Kind = ThesisSectionKind.Cover, Blocks = [] },
                new ThesisSection
                {
                    Kind = ThesisSectionKind.Body,
                    Blocks = [new ParagraphBlock { Inlines = [new TextInline { Text = "正文" }] }]
                }
            ]
        };
        var context = new DocxRenderContext
        {
            TemplateId = "rule-template",
            TemplateVersion = "1.0.0",
            PageTemplates =
            [
                new TemplatePageLayout
                {
                    Id = "rule-cover",
                    TargetSectionType = PageTemplateTargetSectionType.Cover,
                    InsertPosition = PageTemplateInsertPosition.ReplaceSectionContent,
                    Blocks =
                    [
                        new TextLayoutBlock { Value = "Rule Cover", Style = ThesisDocx.Core.OpenXml.StyleIds.TocTitle },
                        new RuleLayoutBlock { ThicknessPt = 2, Color = "333333", SpacingAfterPt = 6 },
                        new PageBreakLayoutBlock()
                    ]
                }
            ]
        };

        var rendered = TestRenderHelper.RenderDocument(document, CreateFormat(), context);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        Assert.Contains(package.MainDocumentPart!.Document.Descendants<W.ParagraphBorders>(),
            borders => borders.BottomBorder?.Val?.Value == W.BorderValues.Single
                && borders.BottomBorder.Size?.Value == 16U
                && borders.BottomBorder.Color?.Value == "333333");

        var inspect = new DocxInspector().Inspect(rendered.DocxPath);
        Assert.Contains("rule-cover", inspect.TemplateRendering.RenderedPageTemplates);
        Assert.Equal(1, inspect.TemplateRendering.RuleParagraphCount);
    }

    [Fact]
    public void RenderPageTemplateFieldsAndHandwritingArea_ShouldEmitFontsRowHeightAndBorders()
    {
        var document = new ThesisDocument
        {
            Metadata = new ThesisMetadata
            {
                Title = "模板字段",
                Author = "测试作者",
                College = "示例学院",
                Major = "导演",
                StudentId = "20260002",
                Advisor = "指导教师",
                Date = "2026-05-15"
            },
            Sections =
            [
                new ThesisSection { Kind = ThesisSectionKind.Cover, Blocks = [] },
                new ThesisSection { Kind = ThesisSectionKind.Body, Blocks = [new ParagraphBlock { Inlines = [new TextInline { Text = "正文" }] }] }
            ]
        };
        var labelFont = new FontFormatSpec { EastAsia = "黑体", Latin = "Times New Roman", SizePt = 16, Bold = true };
        var valueFont = new FontFormatSpec { EastAsia = "楷体", Latin = "Times New Roman", SizePt = 16 };
        var context = new DocxRenderContext
        {
            TemplateId = "field-template",
            TemplateVersion = "1.0.0",
            PageTemplates =
            [
                new TemplatePageLayout
                {
                    Id = "field-cover",
                    TargetSectionType = PageTemplateTargetSectionType.Cover,
                    InsertPosition = PageTemplateInsertPosition.ReplaceSectionContent,
                    Blocks =
                    [
                        new FieldTableLayoutBlock
                        {
                            RowHeightPt = 24,
                            LabelFont = labelFont,
                            ValueFont = valueFont,
                            Rows =
                            [
                                [
                                    new MetadataFieldLayoutBlock { Label = "作者", SourcePath = "metadata.author" },
                                    new MetadataFieldLayoutBlock { Label = "学号", SourcePath = "metadata.studentId" }
                                ]
                            ]
                        },
                        new HandwritingAreaLayoutBlock { Label = "指导教师评语", HeightCm = 3.5, BorderThicknessPt = 1.5, BorderColor = "444444" },
                        new PageBreakLayoutBlock()
                    ]
                }
            ]
        };

        var rendered = TestRenderHelper.RenderDocument(document, CreateFormat(), context);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var tables = package.MainDocumentPart!.Document.Descendants<W.Table>().ToList();
        Assert.True(tables.Count >= 2);
        Assert.Contains(tables[0].Descendants<W.TableRowHeight>(), height => height.Val?.Value == (uint)UnitConverter.PointsToTwips(24));
        Assert.Contains(tables[0].Descendants<W.RunFonts>(), fonts => fonts.EastAsia?.Value == "黑体");
        Assert.Contains(tables[0].Descendants<W.RunFonts>(), fonts => fonts.EastAsia?.Value == "楷体");
        Assert.Contains(tables[0].Descendants<W.FontSize>(), size => size.Val?.Value == UnitConverter.PointsToHalfPoints(16).ToString());

        var handwritingBorders = tables[1].GetFirstChild<W.TableProperties>()!.GetFirstChild<W.TableBorders>()!;
        Assert.Equal(W.BorderValues.Single, handwritingBorders.TopBorder?.Val?.Value);
        Assert.Equal(12U, handwritingBorders.TopBorder?.Size?.Value);
        Assert.Equal("444444", handwritingBorders.TopBorder?.Color?.Value);
        Assert.Contains(tables[1].Descendants<W.TableRowHeight>(), height => height.Val?.Value == (uint)UnitConverter.CentimetersToTwips(3.5));
        Assert.Contains(tables[1].Descendants<W.Text>(), text => text.Text == "指导教师评语");
    }

    [Fact]
    public void RenderPageTemplateTextFormatting_ShouldHonorParagraphFontAndSkipEmptyText()
    {
        var document = new ThesisDocument
        {
            Metadata = new ThesisMetadata
            {
                Title = "页模板文本格式",
                Author = "测试作者",
                College = "示例学院",
                Major = "表演",
                StudentId = "20260003",
                Advisor = "指导教师",
                Date = "2026-05-15"
            },
            Sections =
            [
                new ThesisSection { Kind = ThesisSectionKind.Cover, Blocks = [] },
                new ThesisSection { Kind = ThesisSectionKind.Body, Blocks = [new ParagraphBlock { Inlines = [new TextInline { Text = "正文" }] }] }
            ]
        };
        var labelFont = new FontFormatSpec { EastAsia = "宋体", Latin = "Times New Roman", SizePt = 16, Bold = false };
        var context = new DocxRenderContext
        {
            TemplateId = "text-template",
            TemplateVersion = "1.0.0",
            PageTemplates =
            [
                new TemplatePageLayout
                {
                    Id = "text-cover",
                    TargetSectionType = PageTemplateTargetSectionType.Cover,
                    InsertPosition = PageTemplateInsertPosition.ReplaceSectionContent,
                    Blocks =
                    [
                        new TextLayoutBlock
                        {
                            Value = "精确文字块",
                            FontOverride = new FontFormatSpec { EastAsia = "黑体", Latin = "Times New Roman", SizePt = 18, Bold = true, Italic = true },
                            Paragraph = new ParagraphFormatSpec { LineSpacingExactPt = 24, FirstLineIndentChars = 1, Alignment = TextAlignment.Center }
                        },
                        new TextLayoutBlock { Value = "", SkipWhenEmpty = true },
                        new FieldTableLayoutBlock
                        {
                            LabelFont = labelFont,
                            ValueFont = new FontFormatSpec { EastAsia = "楷体", Latin = "Times New Roman", SizePt = 16 },
                            Rows = [[new MetadataFieldLayoutBlock { Label = "标签", SourcePath = "metadata.author" }]]
                        }
                    ]
                }
            ]
        };

        var rendered = TestRenderHelper.RenderDocument(document, CreateFormat(), context);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var textParagraph = package.MainDocumentPart!.Document.Descendants<W.Paragraph>()
            .Single(paragraph => TextOf(paragraph) == "精确文字块");
        var runProperties = textParagraph.Descendants<W.RunProperties>().First();
        Assert.Contains(runProperties.Elements<W.RunFonts>(), fonts => fonts.EastAsia?.Value == "黑体");
        Assert.Contains(runProperties.Elements<W.FontSize>(), size => size.Val?.Value == UnitConverter.PointsToHalfPoints(18).ToString());
        Assert.NotNull(runProperties.GetFirstChild<W.Bold>());
        Assert.NotNull(runProperties.GetFirstChild<W.Italic>());
        Assert.Equal("480", textParagraph.ParagraphProperties!.SpacingBetweenLines!.Line!.Value);
        Assert.Equal(W.LineSpacingRuleValues.Exact, textParagraph.ParagraphProperties.SpacingBetweenLines.LineRule!.Value);
        Assert.Equal(UnitConverter.PointsToTwips(18).ToString(), textParagraph.ParagraphProperties.Indentation!.FirstLine!.Value);

        Assert.DoesNotContain(package.MainDocumentPart.Document.Descendants<W.Paragraph>(), paragraph => TextOf(paragraph) == string.Empty
            && paragraph.Descendants<W.Run>().Any(run => run.Descendants<W.Text>().Any()));

        var labelRunProperties = package.MainDocumentPart.Document.Descendants<W.TableCell>()
            .First(cell => TextOf(cell) == "标签")
            .Descendants<W.RunProperties>()
            .First();
        Assert.Null(labelRunProperties.GetFirstChild<W.Bold>());
    }

    [Fact]
    public void InspectTemplateRendering_ShouldRecognizeGuidingTeacherAndOriginalityDeclaration()
    {
        var document = new ThesisDocument
        {
            Metadata = new ThesisMetadata
            {
                Title = "声明识别",
                Author = "测试作者",
                College = "示例学院",
                Major = "表演",
                StudentId = "20260004",
                Advisor = "指导教师",
                Date = "2026-05-15"
            },
            Sections =
            [
                new ThesisSection { Kind = ThesisSectionKind.Cover, Blocks = [] },
                new ThesisSection { Kind = ThesisSectionKind.OriginalityStatement, Blocks = [] },
                new ThesisSection { Kind = ThesisSectionKind.Body, Blocks = [new ParagraphBlock { Inlines = [new TextInline { Text = "正文" }] }] }
            ]
        };
        var context = new DocxRenderContext
        {
            TemplateId = "declaration-template",
            TemplateVersion = "1.0.0",
            PageTemplates =
            [
                new TemplatePageLayout
                {
                    Id = "cover-template",
                    TargetSectionType = PageTemplateTargetSectionType.Cover,
                    InsertPosition = PageTemplateInsertPosition.ReplaceSectionContent,
                    Blocks =
                    [
                        new FieldTableLayoutBlock
                        {
                            Rows =
                            [
                                [new MetadataFieldLayoutBlock { Label = "作者", SourcePath = "metadata.author" }],
                                [new MetadataFieldLayoutBlock { Label = "学　　号", SourcePath = "metadata.studentId" }],
                                [new MetadataFieldLayoutBlock { Label = "指导教师", SourcePath = "metadata.advisor" }]
                            ]
                        }
                    ]
                },
                new TemplatePageLayout
                {
                    Id = "declaration-template",
                    TargetSectionType = PageTemplateTargetSectionType.Declaration,
                    InsertPosition = PageTemplateInsertPosition.ReplaceSectionContent,
                    Blocks =
                    [
                        new TextLayoutBlock { Value = "独创性声明" },
                        new TextLayoutBlock { Value = "本人郑重声明：所呈交的论文是本人完成的研究成果。" }
                    ]
                }
            ]
        };

        var rendered = TestRenderHelper.RenderDocument(document, CreateFormat(), context);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        var inspect = new DocxInspector().Inspect(rendered.DocxPath);
        Assert.True(inspect.TemplateRendering.CoverSummary.HasMetadataFieldTable);
        Assert.True(inspect.TemplateRendering.DeclarationSummary.HasDeclarationText);
    }

    [Fact]
    public void RenderOddEvenFootersAndNoteSettings_ShouldEmitSettingsReferencesAndAlignment()
    {
        var format = CreateFormat();
        format.HeaderFooter = new HeaderFooterFormatSpec
        {
            HeaderText = "奇偶页测试",
            DifferentOddEven = true,
            OddPageNumberAlignment = TextAlignment.Right,
            EvenPageNumberAlignment = TextAlignment.Left
        };
        format.Notes.Footnote.NumberFormat = NoteNumberFormat.DecimalEnclosedCircle;
        format.Notes.Footnote.NumberingRestart = NoteNumberingRestart.EachPage;
        format.Notes.Footnote.StartNumber = 1;
        var document = CreateBaseDocument([
            new ParagraphBlock
            {
                Inlines =
                [
                    new TextInline { Text = "正文" },
                    new FootnoteInline { NoteId = "fn-circle", Inlines = [new TextInline { Text = "脚注内容" }] }
                ]
            }
        ]);

        var rendered = TestRenderHelper.RenderDocument(document, format);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var settings = package.MainDocumentPart!.DocumentSettingsPart!.Settings!;
        Assert.True(settings.GetFirstChild<W.EvenAndOddHeaders>()!.Val!.Value);
        var footnoteProperties = settings.GetFirstChild<W.FootnoteDocumentWideProperties>()!;
        Assert.Equal(W.NumberFormatValues.DecimalEnclosedCircle, footnoteProperties.GetFirstChild<W.NumberingFormat>()!.Val!.Value);
        Assert.Equal(1, footnoteProperties.GetFirstChild<W.NumberingStart>()!.Val!.Value);
        Assert.Equal(W.RestartNumberValues.EachPage, footnoteProperties.GetFirstChild<W.NumberingRestart>()!.Val!.Value);

        var section = Assert.Single(package.MainDocumentPart.Document.Body!.Descendants<W.SectionProperties>());
        var defaultFooter = Assert.Single(section.Elements<W.FooterReference>(), reference => reference.Type?.Value == W.HeaderFooterValues.Default);
        var evenFooter = Assert.Single(section.Elements<W.FooterReference>(), reference => reference.Type?.Value == W.HeaderFooterValues.Even);
        AssertFooterAlignment(package, defaultFooter, W.JustificationValues.Right);
        AssertFooterAlignment(package, evenFooter, W.JustificationValues.Left);

        Assert.Contains(package.MainDocumentPart.FootnotesPart!.Footnotes!.Descendants<W.Text>(), text => text.Text == "脚注内容");
    }

    [Fact]
    public void RenderSectionInstanceTitleAndBlockOverrides_ShouldEmitDirectXmlFormatting()
    {
        var document = new ThesisDocument
        {
            Metadata = new ThesisMetadata { Title = "摘要样式", Author = "作者", Date = "2026-05-15" },
            Sections =
            [
                new ThesisSection
                {
                    Id = "abstract-cn",
                    Kind = ThesisSectionKind.Abstract,
                    Title = "内容摘要",
                    Blocks =
                    [
                        new ParagraphBlock { Inlines = [new TextInline { Text = "摘要正文" }] },
                        new ParagraphBlock { Inlines = [new TextInline { Text = "关键词：电影；结构" }] }
                    ]
                }
            ]
        };
        var context = new DocxRenderContext
        {
            Overrides = new DocumentOverrides
            {
                SectionInstances = new Dictionary<string, SectionInstanceOverrideSpec>
                {
                    ["abstract-cn"] = new()
                    {
                        TitleFont = new FontOverrideSpec { EastAsia = "黑体", Latin = "Times New Roman", SizePt = 16, Bold = true },
                        TitleParagraph = new ParagraphOverrideSpec { LineSpacingMultiple = 1.0, FirstLineIndentChars = 0, Alignment = TextAlignment.Center },
                        BlockOverrides = new Dictionary<int, BlockFormatOverrideSpec>
                        {
                            [1] = new()
                            {
                                Font = new FontOverrideSpec { EastAsia = "黑体", Latin = "Times New Roman", SizePt = 14, Bold = true },
                                Paragraph = new ParagraphOverrideSpec { LineSpacingMultiple = 1.0, FirstLineIndentChars = 0, Alignment = TextAlignment.Left }
                            }
                        }
                    }
                }
            }
        };

        var rendered = TestRenderHelper.RenderDocument(document, CreateFormat(), context);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var paragraphs = package.MainDocumentPart!.Document.Descendants<W.Paragraph>().ToList();
        var title = paragraphs.Single(paragraph => TextOf(paragraph) == "内容摘要");
        Assert.Contains(title.Descendants<W.RunFonts>(), fonts => fonts.EastAsia?.Value == "黑体");
        Assert.Contains(title.Descendants<W.FontSize>(), size => size.Val?.Value == UnitConverter.PointsToHalfPoints(16).ToString());
        Assert.Equal("240", title.ParagraphProperties!.SpacingBetweenLines!.Line!.Value);

        var keywords = paragraphs.Single(paragraph => TextOf(paragraph).Contains("关键词", StringComparison.Ordinal));
        Assert.Contains(keywords.Descendants<W.RunFonts>(), fonts => fonts.EastAsia?.Value == "黑体");
        Assert.Contains(keywords.Descendants<W.FontSize>(), size => size.Val?.Value == UnitConverter.PointsToHalfPoints(14).ToString());
        Assert.Equal("240", keywords.ParagraphProperties!.SpacingBetweenLines!.Line!.Value);
        Assert.Null(keywords.ParagraphProperties.Indentation?.FirstLine);
    }

    [Fact]
    public void RenderBibliographyEntryFont_ShouldEmitStyleAndRunFonts()
    {
        var format = CreateFormat();
        format.Bibliography.EntryFont = new FontFormatSpec { EastAsia = "宋体", Latin = "Times New Roman", SizePt = 9 };
        var document = CreateBaseDocument([
            new BibliographyBlock
            {
                Entries =
                [
                    new BibliographyEntryNode { Id = "ref1", Text = "作者. 文献题名[M]. 北京：出版社，2020." },
                    new BibliographyEntryNode { Id = "ref2", Text = "作者. 文献题名[J]. 刊名，2021." }
                ]
            }
        ]);

        var rendered = TestRenderHelper.RenderDocument(document, format);
        AssertNoOpenXmlErrors(rendered.DocxPath);

        using var package = WordprocessingDocument.Open(rendered.DocxPath, false);
        var style = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>()
            .Single(s => s.StyleId?.Value == ThesisDocx.Core.OpenXml.StyleIds.Bibliography);
        Assert.Equal("18", style.GetFirstChild<W.StyleRunProperties>()!.GetFirstChild<W.FontSize>()!.Val!.Value);
        Assert.Equal("宋体", style.GetFirstChild<W.StyleRunProperties>()!.GetFirstChild<W.RunFonts>()!.EastAsia!.Value);
        var bibliographyParagraph = package.MainDocumentPart.Document.Descendants<W.Paragraph>()
            .First(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value == ThesisDocx.Core.OpenXml.StyleIds.Bibliography);
        Assert.Contains(bibliographyParagraph.Descendants<W.FontSize>(), size => size.Val?.Value == "18");
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

    private static ThesisDocument CreateRichCellTableDocument()
    {
        var table = new TableBlock
        {
            Caption = "单元格块",
            Style = TableStyleKind.Custom,
            Rows =
            [
                new TableRowNode
                {
                    Cells =
                    [
                        new TableCellNode
                        {
                            Alignment = TextAlignment.Left,
                            Font = new FontFormatSpec
                            {
                                EastAsia = "宋体",
                                Latin = "Times New Roman",
                                SizePt = 9
                            },
                            Paragraph = new ParagraphFormatSpec
                            {
                                LineSpacingMultiple = 1.0,
                                SpaceBeforePt = 0,
                                SpaceAfterPt = 0,
                                FirstLineIndentChars = 0,
                                HangingIndentCm = 0,
                                Alignment = TextAlignment.Left,
                                WidowControl = true
                            },
                            Blocks =
                            [
                                new ParagraphBlock
                                {
                                    Inlines =
                                    [
                                        new TextInline { Text = "单元格段落" },
                                        new HyperlinkInline { Text = "链接", Uri = "https://example.com" },
                                        new FootnoteInline { NoteId = "fn-cell", Inlines = [new TextInline { Text = "单元格脚注" }] }
                                    ]
                                },
                                new HeadingBlock { Level = 2, Numbered = false, Inlines = [new TextInline { Text = "单元格标题" }] },
                                new QuoteBlock { Inlines = [new TextInline { Text = "单元格引用" }] },
                                new ListBlock
                                {
                                    Ordered = false,
                                    Items =
                                    [
                                        new ListItemNode { Blocks = [new ParagraphBlock { Inlines = [new TextInline { Text = "列表项" }] }] }
                                    ]
                                }
                            ]
                        }
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

    private static void AssertFooterAlignment(WordprocessingDocument package, W.FooterReference reference, W.JustificationValues expected)
    {
        var footerPart = Assert.IsType<FooterPart>(package.MainDocumentPart!.GetPartById(reference.Id!));
        var paragraph = footerPart.Footer.Descendants<W.Paragraph>()
            .First(p => p.Descendants<W.SimpleField>().Any(field => field.Instruction?.Value?.Contains("PAGE", StringComparison.OrdinalIgnoreCase) == true));
        Assert.Equal(expected, paragraph.ParagraphProperties!.Justification!.Val!.Value);
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
