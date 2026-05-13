using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;
using ThesisDocx.Tests.OpenXmlAssertions;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class DocumentOverridesTests
{
    [Fact]
    public void DocumentOverridesFormatMerger_ShouldApplyGlobalAndSectionBucketOverrides()
    {
        var format = new ThesisFormatSpec();
        var merged = new DocumentOverridesFormatMerger().Merge(format, new DocumentOverrides
        {
            Toc = new TocOverrideSpec { Title = "本文目录", MinLevel = 1, MaxLevel = 2 },
            HeaderFooter = new HeaderFooterOverrideSpec { HeaderText = "覆盖页眉", DrawHeaderLine = false },
            DefaultFont = new FontOverrideSpec { EastAsia = "黑体", SizePt = 10.5 },
            BodyParagraph = new ParagraphOverrideSpec { LineSpacingMultiple = 1.25, LineSpacingExactPt = 18, FirstLineIndentChars = 0 },
            Headings = new Dictionary<int, HeadingOverrideSpec>
            {
                [1] = new() { Alignment = TextAlignment.Center, Font = new FontOverrideSpec { SizePt = 18, Bold = true } }
            },
            SectionFormats = new Dictionary<string, SectionFormatOverrideSpec>
            {
                ["body"] = new() { StartPageNumber = 7, RestartPageNumbering = true }
            }
        });

        Assert.Equal("本文目录", merged.Toc.Title);
        Assert.Equal(2, merged.Toc.MaxLevel);
        Assert.Equal("覆盖页眉", merged.HeaderFooter.HeaderText);
        Assert.False(merged.HeaderFooter.DrawHeaderLine);
        Assert.Equal("黑体", merged.DefaultFont.EastAsia);
        Assert.Equal(10.5, merged.DefaultFont.SizePt);
        Assert.Equal(1.25, merged.BodyParagraph.LineSpacingMultiple);
        Assert.Equal(18, merged.BodyParagraph.LineSpacingExactPt);
        Assert.Equal(TextAlignment.Center, merged.Headings[1].Alignment);
        Assert.Equal(18, merged.Headings[1].Font.SizePt);
        Assert.Equal(7, merged.Sections["body"].StartPageNumber);
    }

    [Fact]
    public void RenderWithDocumentOverrides_ShouldApplyTocHeaderFooterPageNumberAndStyles()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var document = JsonSerializer.Deserialize<ThesisDocument>(
            File.ReadAllText(Path.Combine(root, "examples", "simple-thesis", "document.json")),
            ThesisJson.Options)!;
        var format = JsonSerializer.Deserialize<ThesisFormatSpec>(
            File.ReadAllText(Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json")),
            ThesisJson.Options)!;
        var overrides = new DocumentOverrides
        {
            Toc = new TocOverrideSpec
            {
                Title = "本文目录",
                MinLevel = 1,
                MaxLevel = 2,
                IncludeSectionIds = ["body"]
            },
            HeaderFooter = new HeaderFooterOverrideSpec
            {
                HeaderText = "全局覆盖页眉",
                DrawHeaderLine = false
            },
            DefaultFont = new FontOverrideSpec { EastAsia = "黑体", Latin = "Arial", SizePt = 11 },
            BodyParagraph = new ParagraphOverrideSpec { LineSpacingMultiple = 1.25, LineSpacingExactPt = 18 },
            SectionInstances = new Dictionary<string, SectionInstanceOverrideSpec>
            {
                ["body"] = new()
                {
                    HeaderText = "正文专用页眉",
                    FooterText = "正文页",
                    PageNumberStyle = PageNumberStyle.Decimal,
                    StartPageNumber = 7,
                    RestartPageNumbering = true,
                    Paragraph = new ParagraphOverrideSpec { LineSpacingMultiple = 2.0, FirstLineIndentChars = 0 },
                    DefaultFont = new FontOverrideSpec { EastAsia = "楷体", Latin = "Arial", SizePt = 10.5 }
                }
            }
        };

        var rendered = TestRenderHelper.RenderDocument(document, format, new DocxRenderContext { Overrides = overrides });
        var openXml = new OpenXmlPackageValidator().Validate(rendered.DocxPath);

        Assert.True(openXml.IsValid, string.Join(Environment.NewLine, openXml.Errors));

        using var package = OpenXmlAssert.OpenWordDocument(rendered.DocxPath);
        var body = package.MainDocumentPart!.Document.Body!;

        Assert.Contains(body.Descendants<W.Text>(), text => text.Text == "本文目录");
        var tocField = Assert.Single(body.Descendants<W.SimpleField>(), field => field.Instruction?.Value?.StartsWith("TOC", StringComparison.Ordinal) == true);
        Assert.Contains("\\o \"1-2\"", tocField.Instruction!.Value);
        Assert.Contains("\\b _TocSec_body", tocField.Instruction!.Value);
        Assert.Contains(body.Descendants<W.BookmarkStart>(), bookmark => bookmark.Name?.Value == "_TocSec_body");

        var bodyStyle = OpenXmlAssert.RequiredStyle(package, StyleIds.ThesisBody);
        var fonts = bodyStyle.GetFirstChild<W.StyleRunProperties>()!.GetFirstChild<W.RunFonts>()!;
        Assert.Equal("黑体", fonts.EastAsia?.Value);
        Assert.Equal("Arial", fonts.Ascii?.Value);
        var bodySpacing = bodyStyle.GetFirstChild<W.StyleParagraphProperties>()!.GetFirstChild<W.SpacingBetweenLines>()!;
        Assert.Equal("360", bodySpacing.Line!.Value);
        Assert.Equal(W.LineSpacingRuleValues.Exact, bodySpacing.LineRule!.Value);

        var bodySection = body.Descendants<W.SectionProperties>()
            .First(sp => sp.GetFirstChild<W.PageNumberType>()?.Start?.Value == 7);
        Assert.Equal(W.NumberFormatValues.Decimal, bodySection.GetFirstChild<W.PageNumberType>()!.Format!.Value);

        Assert.Contains(package.MainDocumentPart.HeaderParts.SelectMany(part => part.Header.Descendants<W.Text>()),
            text => text.Text == "正文专用页眉");
        Assert.Contains(package.MainDocumentPart.FooterParts.SelectMany(part => part.Footer.Descendants<W.Text>()),
            text => text.Text.Contains("正文页", StringComparison.Ordinal));

        var paragraphWithSectionFont = body.Descendants<W.Paragraph>()
            .FirstOrDefault(paragraph => paragraph.Descendants<W.RunFonts>().Any(font => font.EastAsia?.Value == "楷体"));
        Assert.NotNull(paragraphWithSectionFont);
        Assert.Contains(paragraphWithSectionFont!.Descendants<W.SpacingBetweenLines>(), spacing => spacing.Line?.Value == "480");
    }

    [Fact]
    public void DocumentOverridesValidator_ShouldRejectInvalidRanges()
    {
        var diagnostics = new DocumentOverridesValidator().Validate(new DocumentOverrides
        {
            Toc = new TocOverrideSpec { MinLevel = 4, MaxLevel = 2 },
            DefaultFont = new FontOverrideSpec { SizePt = 0 },
            BodyParagraph = new ParagraphOverrideSpec { LineSpacingExactPt = 0 },
            SectionFormats = new Dictionary<string, SectionFormatOverrideSpec>
            {
                ["body"] = new() { StartPageNumber = 0 }
            }
        });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "overrides.toc.levelRange");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "overrides.font.size.range");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "overrides.paragraph.lineSpacingExact.range");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "overrides.section.startPageNumber.range");
    }
}
