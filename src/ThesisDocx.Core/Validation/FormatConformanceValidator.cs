using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Utilities;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace ThesisDocx.Core.Validation;

public sealed class FormatConformanceValidator
{
    public OpenXmlValidationResult Validate(string docxPath, string templatePath)
    {
        var resolution = new TemplateResolver().Resolve(templatePath);
        var result = Validate(docxPath, resolution.FormatSpec ?? new ThesisFormatSpec());
        ValidateTemplateRendering(docxPath, resolution, result);
        return result;
    }

    public OpenXmlValidationResult Validate(string docxPath, ThesisFormatSpec format)
    {
        var result = new OpenXmlPackageValidator().Validate(docxPath);
        using var document = WordprocessingDocument.Open(docxPath, false);
        var mainPart = document.MainDocumentPart;
        if (mainPart is null)
        {
            AddError(result, "missing.mainPart", "Missing main document part.", "/word/document.xml", "/", null, null);
            return result;
        }

        ValidatePageSetup(mainPart, format, result);
        ValidateStyles(mainPart, format, result);
        ValidateHeadingNumbering(mainPart, format, result);
        ValidateToc(mainPart, format, result);
        ValidateHeaderFooter(mainPart, format, result);
        ValidateNotes(mainPart, format, result);
        ValidateBibliography(mainPart, format, result);
        ValidateReferences(mainPart, result);
        ValidateEquations(mainPart, format, result);
        ValidateTables(mainPart, format, result);
        ValidateFigures(mainPart, result);

        return result;
    }

    private static void ValidateTemplateRendering(string docxPath, Models.Templates.TemplateResolutionResult resolution, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("template.cover");
        result.CheckedRules.Add("template.declaration");
        result.CheckedRules.Add("template.assets");
        result.CheckedRules.Add("template.variables");

        var inspection = new DocxInspector().Inspect(docxPath);
        if (!string.IsNullOrWhiteSpace(resolution.Template?.Id)
            && !string.Equals(inspection.TemplateRendering.TemplateId, resolution.Template.Id, StringComparison.Ordinal))
        {
            AddError(result, "template.properties.templateId", "Rendered DOCX custom template id does not match template.", "/docProps/custom.xml", "//property[@name='ThesisDocx.TemplateId']", resolution.Template.Id, inspection.TemplateRendering.TemplateId);
        }

        foreach (var layout in resolution.PageTemplates.Where(RequiresRenderedPageTemplate))
        {
            if (!inspection.TemplateRendering.RenderedPageTemplates.Contains(layout.Id, StringComparer.Ordinal))
            {
                AddError(result, "template.pageTemplate.missing", $"Page template '{layout.Id}' was not rendered.", "/docProps/custom.xml", "ThesisDocx.RenderedPageTemplates", layout.Id, string.Join(",", inspection.TemplateRendering.RenderedPageTemplates));
            }
        }

        foreach (var asset in resolution.Assets.Where(asset => asset.Required && asset.Type == Models.Templates.TemplateAssetType.Image))
        {
            if (!inspection.TemplateRendering.RenderedAssets.Contains(asset.Id, StringComparer.Ordinal))
            {
                AddError(result, "template.asset.notRendered", $"Required asset '{asset.Id}' was not rendered.", "/docProps/custom.xml", "ThesisDocx.RenderedAssets", asset.Id, string.Join(",", inspection.TemplateRendering.RenderedAssets));
            }
        }

        if (resolution.PageTemplates.Any(p => p.TargetSectionType == Models.Templates.PageTemplateTargetSectionType.Cover)
            && !inspection.TemplateRendering.CoverSummary.HasMetadataFieldTable)
        {
            AddError(result, "template.cover.requiredFieldsMissing", "Cover template required metadata fields are missing.", "/word/document.xml", "//w:tbl", "cover metadata table", "missing");
        }

        if (resolution.PageTemplates.Any(p => p.TargetSectionType == Models.Templates.PageTemplateTargetSectionType.Declaration)
            && !inspection.TemplateRendering.DeclarationSummary.HasDeclarationText)
        {
            AddError(result, "template.declaration.textMissing", "Declaration template text is missing.", "/word/document.xml", "//w:t", "declaration text", "missing");
        }
    }

    private static bool RequiresRenderedPageTemplate(Models.Templates.TemplatePageLayout layout)
    {
        return layout.TargetSectionType != Models.Templates.PageTemplateTargetSectionType.Appendix;
    }

    private static void ValidatePageSetup(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("page.margins");
        result.CheckedRules.Add("page.size");

        var sectionProperties = mainPart.Document.Body?.Descendants<W.SectionProperties>().ToList() ?? [];
        if (sectionProperties.Count == 0)
        {
            AddError(result, "missing.sectionProperties", "Missing section properties.", "/word/document.xml", "//w:sectPr", "at least one sectPr", "none");
            return;
        }

        var expectedTop = UnitConverter.CentimetersToTwips(format.PageSetup.TopMarginCm).ToString();
        var expectedBottom = UnitConverter.CentimetersToTwips(format.PageSetup.BottomMarginCm).ToString();
        var expectedLeft = UnitConverter.CentimetersToTwips(format.PageSetup.LeftMarginCm).ToString();
        var expectedRight = UnitConverter.CentimetersToTwips(format.PageSetup.RightMarginCm).ToString();
        var firstMargin = sectionProperties[0].GetFirstChild<W.PageMargin>();
        Compare(result, "page.margin.top", "/word/document.xml", "//w:sectPr[1]/w:pgMar/@w:top", expectedTop, firstMargin?.Top?.Value.ToString());
        Compare(result, "page.margin.bottom", "/word/document.xml", "//w:sectPr[1]/w:pgMar/@w:bottom", expectedBottom, firstMargin?.Bottom?.Value.ToString());
        Compare(result, "page.margin.left", "/word/document.xml", "//w:sectPr[1]/w:pgMar/@w:left", expectedLeft, firstMargin?.Left?.Value.ToString());
        Compare(result, "page.margin.right", "/word/document.xml", "//w:sectPr[1]/w:pgMar/@w:right", expectedRight, firstMargin?.Right?.Value.ToString());

        var expectedPage = format.PageSetup.PaperSize == PaperSizeKind.A4
            ? (Width: UnitConverter.MillimetersToTwips(210).ToString(), Height: UnitConverter.MillimetersToTwips(297).ToString())
            : (Width: UnitConverter.InchesToTwips(8.5).ToString(), Height: UnitConverter.InchesToTwips(11).ToString());
        if (format.PageSetup.Orientation == PageOrientationKind.Landscape)
        {
            expectedPage = (expectedPage.Height, expectedPage.Width);
        }

        var firstPageSize = sectionProperties[0].GetFirstChild<W.PageSize>();
        Compare(result, "page.size.width", "/word/document.xml", "//w:sectPr[1]/w:pgSz/@w:w", expectedPage.Width, firstPageSize?.Width?.Value.ToString());
        Compare(result, "page.size.height", "/word/document.xml", "//w:sectPr[1]/w:pgSz/@w:h", expectedPage.Height, firstPageSize?.Height?.Value.ToString());

        result.CheckedRules.Add("page.numbering");
        var pageNumberFormats = sectionProperties
            .Select(sp => sp.GetFirstChild<W.PageNumberType>())
            .Where(pn => pn?.Format is not null)
            .Select(pn => pn!.Format!.Value)
            .ToList();

        var expectedPageNumberFormats = format.Sections.Values
            .Where(section => section.PageNumberStyle != PageNumberStyle.None)
            .Select(section => ToNumberFormat(section.PageNumberStyle))
            .Distinct()
            .ToList();
        var missingPageNumberFormats = expectedPageNumberFormats
            .Where(expected => !pageNumberFormats.Any(actual => actual == expected))
            .Select(expected => expected.ToString())
            .Order(StringComparer.Ordinal)
            .ToList();

        if (missingPageNumberFormats.Count > 0)
        {
            AddError(
                result,
                "page.numbering.missingProfiles",
                "Expected configured page numbering section profiles.",
                "/word/document.xml",
                "//w:sectPr/w:pgNumType",
                string.Join(",", expectedPageNumberFormats.Select(value => value.ToString()).Order(StringComparer.Ordinal)),
                string.Join(",", pageNumberFormats));
        }
    }

    private static void ValidateStyles(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("styles.required");
        result.CheckedRules.Add("styles.defaultFonts");
        result.CheckedRules.Add("styles.bodyLineSpacing");
        result.CheckedRules.Add("styles.headings");

        var styles = mainPart.StyleDefinitionsPart?.Styles;
        var styleIds = styles?.Elements<W.Style>()
            .Select(s => s.StyleId?.Value)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.Ordinal)
            ?? [];

        var requiredStyles = new[]
        {
            StyleIds.ThesisBody,
            StyleIds.Heading1,
            StyleIds.Heading2,
            StyleIds.Heading3,
            StyleIds.Caption,
            StyleIds.Bibliography,
            StyleBuilder.NoteStyleId(format.Notes.Footnote, StyleIds.FootnoteText),
            StyleBuilder.NoteStyleId(format.Notes.Endnote, StyleIds.EndnoteText)
        };
        foreach (var required in requiredStyles.Distinct(StringComparer.Ordinal))
        {
            if (!styleIds.Contains(required))
            {
                AddError(result, "styles.required.missing", $"Missing required style '{required}'.", "/word/styles.xml", $"//w:style[@w:styleId='{required}']", required, "missing");
            }
        }

        var bodyStyle = styles?.Elements<W.Style>().FirstOrDefault(s => s.StyleId?.Value == StyleIds.ThesisBody);
        var runFonts = bodyStyle?.GetFirstChild<W.StyleRunProperties>()?.GetFirstChild<W.RunFonts>();
        Compare(result, "styles.defaultFonts.eastAsia", "/word/styles.xml", $"//w:style[@w:styleId='{StyleIds.ThesisBody}']//w:rFonts/@w:eastAsia", format.DefaultFont.EastAsia, runFonts?.EastAsia?.Value);
        Compare(result, "styles.defaultFonts.latin", "/word/styles.xml", $"//w:style[@w:styleId='{StyleIds.ThesisBody}']//w:rFonts/@w:ascii", format.DefaultFont.Latin, runFonts?.Ascii?.Value);

        var spacing = bodyStyle?.GetFirstChild<W.StyleParagraphProperties>()?.GetFirstChild<W.SpacingBetweenLines>();
        var expectedLine = ExpectedLineSpacing(format.BodyParagraph);
        var expectedLineRule = ExpectedLineSpacingRule(format.BodyParagraph);
        Compare(result, "styles.body.lineSpacing", "/word/styles.xml", $"//w:style[@w:styleId='{StyleIds.ThesisBody}']//w:spacing/@w:line", expectedLine, spacing?.Line?.Value);
        Compare(result, "styles.body.lineSpacingRule", "/word/styles.xml", $"//w:style[@w:styleId='{StyleIds.ThesisBody}']//w:spacing/@w:lineRule", expectedLineRule, ActualLineRule(spacing));

        var bibliographyStyle = styles?.Elements<W.Style>().FirstOrDefault(s => s.StyleId?.Value == StyleIds.Bibliography);
        var bibliographySpacing = bibliographyStyle?.GetFirstChild<W.StyleParagraphProperties>()?.GetFirstChild<W.SpacingBetweenLines>();
        Compare(result, "styles.bibliography.lineSpacing", "/word/styles.xml", $"//w:style[@w:styleId='{StyleIds.Bibliography}']//w:spacing/@w:line", ExpectedLineSpacing(format.Bibliography.EntryParagraph), bibliographySpacing?.Line?.Value);
        Compare(result, "styles.bibliography.lineSpacingRule", "/word/styles.xml", $"//w:style[@w:styleId='{StyleIds.Bibliography}']//w:spacing/@w:lineRule", ExpectedLineSpacingRule(format.Bibliography.EntryParagraph), ActualLineRule(bibliographySpacing));

        foreach (var styleId in new[] { StyleIds.Heading1, StyleIds.Heading2, StyleIds.Heading3 })
        {
            var heading = styles?.Elements<W.Style>().FirstOrDefault(s => s.StyleId?.Value == styleId);
            if (heading?.GetFirstChild<W.StyleParagraphProperties>()?.GetFirstChild<W.OutlineLevel>() is null)
            {
                AddError(result, "styles.heading.outlineMissing", $"Heading style '{styleId}' is missing outline level.", "/word/styles.xml", $"//w:style[@w:styleId='{styleId}']//w:outlineLvl", "outline level", "missing");
            }
        }

        ValidateNoteStyle(styles, format.Notes.Footnote, StyleIds.FootnoteText, "footnote", result);
        ValidateNoteStyle(styles, format.Notes.Endnote, StyleIds.EndnoteText, "endnote", result);
    }

    private static void ValidateNoteStyle(W.Styles? styles, NoteFormatSpec note, string defaultStyleId, string kind, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add($"styles.{kind}");
        var styleId = StyleBuilder.NoteStyleId(note, defaultStyleId);
        var style = styles?.Elements<W.Style>().FirstOrDefault(s => s.StyleId?.Value == styleId);
        if (style is null)
        {
            return;
        }

        var runProperties = style.GetFirstChild<W.StyleRunProperties>();
        Compare(result, $"styles.{kind}.fontSize", "/word/styles.xml", $"//w:style[@w:styleId='{styleId}']//w:sz/@w:val", UnitConverter.PointsToHalfPoints(note.Font.SizePt).ToString(), runProperties?.GetFirstChild<W.FontSize>()?.Val?.Value);
        Compare(result, $"styles.{kind}.fontEastAsia", "/word/styles.xml", $"//w:style[@w:styleId='{styleId}']//w:rFonts/@w:eastAsia", note.Font.EastAsia, runProperties?.GetFirstChild<W.RunFonts>()?.EastAsia?.Value);

        var spacing = style.GetFirstChild<W.StyleParagraphProperties>()?.GetFirstChild<W.SpacingBetweenLines>();
        Compare(result, $"styles.{kind}.lineSpacing", "/word/styles.xml", $"//w:style[@w:styleId='{styleId}']//w:spacing/@w:line", ExpectedLineSpacing(note.Paragraph), spacing?.Line?.Value);
        Compare(result, $"styles.{kind}.lineSpacingRule", "/word/styles.xml", $"//w:style[@w:styleId='{styleId}']//w:spacing/@w:lineRule", ExpectedLineSpacingRule(note.Paragraph), ActualLineRule(spacing));
    }

    private static string ExpectedLineSpacing(ParagraphFormatSpec paragraph)
    {
        return paragraph.LineSpacingExactPt.HasValue
            ? UnitConverter.PointsToTwips(paragraph.LineSpacingExactPt.Value).ToString()
            : ((int)Math.Round(paragraph.LineSpacingMultiple * 240)).ToString();
    }

    private static string ExpectedLineSpacingRule(ParagraphFormatSpec paragraph)
    {
        return paragraph.LineSpacingExactPt.HasValue ? "exact" : "auto";
    }

    private static string? ActualLineRule(W.SpacingBetweenLines? spacing)
    {
        var value = spacing?.LineRule?.Value;
        if (value is null)
        {
            return null;
        }

        if (value == W.LineSpacingRuleValues.Exact) return "exact";
        if (value == W.LineSpacingRuleValues.Auto) return "auto";
        if (value == W.LineSpacingRuleValues.AtLeast) return "atLeast";
        return value.ToString();
    }

    private static void ValidateHeadingNumbering(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("numbering.headings");
        var levelTexts = mainPart.NumberingDefinitionsPart?.Numbering?.Descendants<W.LevelText>()
            .Select(t => t.Val?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.Ordinal) ?? [];

        foreach (var expected in new[] { format.Numbering.HeadingLevel1Text, format.Numbering.HeadingLevel2Text, format.Numbering.HeadingLevel3Text })
        {
            if (!levelTexts.Contains(expected))
            {
                AddError(result, "numbering.headingLevelTextMissing", $"Missing heading numbering text '{expected}'.", "/word/numbering.xml", "//w:lvlText", expected, string.Join(",", levelTexts));
            }
        }
    }

    private static void ValidateToc(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("fields.toc");
        if (!format.Toc.UseWordFieldCode)
        {
            return;
        }

        var hasToc = mainPart.Document.Descendants<W.SimpleField>()
            .Any(f => f.Instruction?.Value?.Contains("TOC", StringComparison.OrdinalIgnoreCase) == true);
        if (!hasToc)
        {
            AddError(result, "fields.toc.missing", "Missing TOC field code.", "/word/document.xml", "//w:fldSimple[contains(@w:instr,'TOC')]", "TOC field", "missing");
        }
    }

    private static void ValidateHeaderFooter(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("header.exists");
        result.CheckedRules.Add("footer.pageNumber");

        var requiresHeader = format.Sections.Values.Any(s => s.IncludeHeader) && !string.IsNullOrWhiteSpace(format.HeaderFooter.HeaderText);
        if (requiresHeader && !mainPart.HeaderParts.Any())
        {
            AddError(result, "header.missing", "Header is required but no header part exists.", "/word/document.xml", "//w:headerReference", "header part", "missing");
        }

        var requiresFooter = format.Sections.Values.Any(s => s.IncludeFooter && s.PageNumberStyle != PageNumberStyle.None);
        var hasPageField = mainPart.FooterParts.Any(fp => fp.Footer.Descendants<W.SimpleField>().Any(f => f.Instruction?.Value?.Contains("PAGE", StringComparison.OrdinalIgnoreCase) == true));
        if (requiresFooter && !hasPageField)
        {
            AddError(result, "footer.pageNumber.missing", "Footer page number field is required but missing.", "/word/footer*.xml", "//w:fldSimple[contains(@w:instr,'PAGE')]", "PAGE field", "missing");
        }

        if (format.HeaderFooter.DifferentOddEven)
        {
            result.CheckedRules.Add("footer.oddEven");
            var settingsHasOddEven = mainPart.DocumentSettingsPart?.Settings?.GetFirstChild<W.EvenAndOddHeaders>()?.Val?.Value == true;
            Compare(result, "footer.oddEven.settings", "/word/settings.xml", "//w:evenAndOddHeaders/@w:val", "true", settingsHasOddEven ? "true" : "false");
            var footerReferences = mainPart.Document.Body?.Descendants<W.FooterReference>().ToList() ?? [];
            var hasEvenFooterReference = footerReferences.Any(reference => reference.Type?.Value == W.HeaderFooterValues.Even);
            if (requiresFooter && !hasEvenFooterReference)
            {
                AddError(result, "footer.oddEven.evenReferenceMissing", "Odd/even page numbering requires an even footer reference.", "/word/document.xml", "//w:footerReference[@w:type='even']", "even footer", "missing");
            }

            ValidateFooterAlignment(mainPart, footerReferences.FirstOrDefault(reference => reference.Type?.Value == W.HeaderFooterValues.Default), format.HeaderFooter.OddPageNumberAlignment, "odd", result);
            ValidateFooterAlignment(mainPart, footerReferences.FirstOrDefault(reference => reference.Type?.Value == W.HeaderFooterValues.Even), format.HeaderFooter.EvenPageNumberAlignment, "even", result);
        }
    }

    private static void ValidateFooterAlignment(MainDocumentPart mainPart, W.FooterReference? reference, TextAlignment expectedAlignment, string label, OpenXmlValidationResult result)
    {
        if (reference?.Id is null)
        {
            return;
        }

        if (mainPart.GetPartById(reference.Id!) is not FooterPart footerPart)
        {
            AddError(result, $"footer.{label}.partMissing", $"Footer relationship '{reference.Id}' does not resolve to a footer part.", "/word/document.xml", "//w:footerReference", reference.Id, "missing");
            return;
        }

        var paragraph = footerPart.Footer.Descendants<W.Paragraph>()
            .FirstOrDefault(p => p.Descendants<W.SimpleField>().Any(field => field.Instruction?.Value?.Contains("PAGE", StringComparison.OrdinalIgnoreCase) == true));
        var actual = paragraph?.ParagraphProperties?.Justification?.Val?.Value;
        Compare(result, $"footer.{label}.alignment", "/word/footer*.xml", "//w:pPr/w:jc/@w:val", StyleBuilder.ToJustification(expectedAlignment).ToString(), actual?.ToString());
    }

    private static void ValidateNotes(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("notes.footnotes");
        result.CheckedRules.Add("notes.endnotes");
        result.CheckedRules.Add("notes.styles");
        result.CheckedRules.Add("notes.settings");

        var footnoteReferences = mainPart.Document.Descendants<W.FootnoteReference>().Select(r => r.Id?.Value).Where(v => v is > 0).Select(v => v!.Value).ToList();
        var footnoteIds = mainPart.FootnotesPart?.Footnotes?.Elements<W.Footnote>().Select(f => f.Id?.Value).Where(v => v.HasValue).Select(v => v!.Value).ToHashSet() ?? [];
        ValidateNoteSet(result, "footnote", "/word/footnotes.xml", footnoteReferences, footnoteIds, mainPart.FootnotesPart is not null);
        ValidateNoteParagraphStyles(result, "footnote", "/word/footnotes.xml", mainPart.FootnotesPart?.Footnotes?.Elements<W.Footnote>().Where(note => note.Id?.Value > 0).SelectMany(note => note.Elements<W.Paragraph>()) ?? [], StyleBuilder.NoteStyleId(format.Notes.Footnote, StyleIds.FootnoteText));

        var endnoteReferences = mainPart.Document.Descendants<W.EndnoteReference>().Select(r => r.Id?.Value).Where(v => v is > 0).Select(v => v!.Value).ToList();
        var endnoteIds = mainPart.EndnotesPart?.Endnotes?.Elements<W.Endnote>().Select(f => f.Id?.Value).Where(v => v.HasValue).Select(v => v!.Value).ToHashSet() ?? [];
        ValidateNoteSet(result, "endnote", "/word/endnotes.xml", endnoteReferences, endnoteIds, mainPart.EndnotesPart is not null);
        ValidateNoteParagraphStyles(result, "endnote", "/word/endnotes.xml", mainPart.EndnotesPart?.Endnotes?.Elements<W.Endnote>().Where(note => note.Id?.Value > 0).SelectMany(note => note.Elements<W.Paragraph>()) ?? [], StyleBuilder.NoteStyleId(format.Notes.Endnote, StyleIds.EndnoteText));
        ValidateNoteSettings(result, "footnote", "/word/settings.xml", mainPart.DocumentSettingsPart?.Settings?.GetFirstChild<W.FootnoteDocumentWideProperties>(), format.Notes.Footnote);
        ValidateNoteSettings(result, "endnote", "/word/settings.xml", mainPart.DocumentSettingsPart?.Settings?.GetFirstChild<W.EndnoteDocumentWideProperties>(), format.Notes.Endnote);
    }

    private static void ValidateNoteSettings(OpenXmlValidationResult result, string kind, string partName, OpenXmlElement? properties, NoteFormatSpec expected)
    {
        var numberingFormat = properties?.GetFirstChild<W.NumberingFormat>()?.Val?.Value;
        var numberingStart = properties?.GetFirstChild<W.NumberingStart>()?.Val?.Value;
        var numberingRestart = properties?.GetFirstChild<W.NumberingRestart>()?.Val?.Value;
        Compare(result, $"notes.{kind}.numberFormat", partName, $"//w:{kind}Pr/w:numFmt/@w:val", ToNoteNumberFormat(expected.NumberFormat).ToString(), numberingFormat?.ToString());
        Compare(result, $"notes.{kind}.numberStart", partName, $"//w:{kind}Pr/w:numStart/@w:val", expected.StartNumber.ToString(), numberingStart?.ToString());
        Compare(result, $"notes.{kind}.numberRestart", partName, $"//w:{kind}Pr/w:numRestart/@w:val", ToNoteRestart(expected.NumberingRestart).ToString(), numberingRestart?.ToString());
    }

    private static void ValidateNoteSet(OpenXmlValidationResult result, string kind, string partName, IReadOnlyList<long> references, HashSet<long> partIds, bool partExists)
    {
        if (references.Count == 0)
        {
            return;
        }

        if (!partExists)
        {
            AddError(result, $"notes.{kind}.partMissing", $"{kind} references exist but {partName} is missing.", partName, "/", $"{kind} part", "missing");
            return;
        }

        if (!partIds.Contains(-1) || !partIds.Contains(0))
        {
            AddError(result, $"notes.{kind}.separatorsMissing", $"{partName} is missing separator or continuation separator.", partName, "/*", "-1 and 0", string.Join(",", partIds.Order()));
        }

        foreach (var referenceId in references)
        {
            if (!partIds.Contains(referenceId))
            {
                AddError(result, $"notes.{kind}.targetMissing", $"{kind} reference id '{referenceId}' has no matching note content.", partName, $"//*[@w:id='{referenceId}']", referenceId.ToString(), "missing");
            }
        }
    }

    private static void ValidateNoteParagraphStyles(OpenXmlValidationResult result, string kind, string partName, IEnumerable<W.Paragraph> paragraphs, string expectedStyleId)
    {
        foreach (var paragraph in paragraphs)
        {
            var actual = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            Compare(result, $"notes.{kind}.style", partName, "//w:p/w:pPr/w:pStyle/@w:val", expectedStyleId, actual);
        }
    }

    private static void ValidateBibliography(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("bibliography.hangingIndent");
        var bibliographyParagraphs = mainPart.Document.Descendants<W.Paragraph>()
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == StyleIds.Bibliography)
            .ToList();

        if (bibliographyParagraphs.Count == 0)
        {
            return;
        }

        var expected = UnitConverter.CentimetersToTwips(format.Bibliography.EntryParagraph.HangingIndentCm).ToString();
        foreach (var paragraph in bibliographyParagraphs)
        {
            var actual = paragraph.ParagraphProperties?.Indentation?.Hanging?.Value;
            Compare(result, "bibliography.hangingIndent", "/word/document.xml", "//w:p[w:pPr/w:pStyle[@w:val='ThesisBibliography']]/w:pPr/w:ind/@w:hanging", expected, actual);
        }

        if (format.Bibliography.EntryFont is not null)
        {
            result.CheckedRules.Add("bibliography.entryFont");
            var style = mainPart.StyleDefinitionsPart?.Styles?.Elements<W.Style>().FirstOrDefault(s => s.StyleId?.Value == StyleIds.Bibliography);
            var runProperties = style?.GetFirstChild<W.StyleRunProperties>();
            Compare(result, "bibliography.entryFont.size", "/word/styles.xml", $"//w:style[@w:styleId='{StyleIds.Bibliography}']//w:sz/@w:val", UnitConverter.PointsToHalfPoints(format.Bibliography.EntryFont.SizePt).ToString(), runProperties?.GetFirstChild<W.FontSize>()?.Val?.Value);
            Compare(result, "bibliography.entryFont.eastAsia", "/word/styles.xml", $"//w:style[@w:styleId='{StyleIds.Bibliography}']//w:rFonts/@w:eastAsia", format.Bibliography.EntryFont.EastAsia, runProperties?.GetFirstChild<W.RunFonts>()?.EastAsia?.Value);
        }
    }

    private static void ValidateReferences(MainDocumentPart mainPart, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("fields.refTargets");
        var bookmarks = mainPart.Document.Descendants<W.BookmarkStart>()
            .Select(b => b.Name?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var reference in mainPart.Document.Descendants<W.SimpleField>())
        {
            var instruction = reference.Instruction?.Value;
            var target = ExtractRefTarget(instruction);
            if (string.IsNullOrWhiteSpace(target) || bookmarks.Contains(target))
            {
                continue;
            }

            var code = target.Contains("eq", StringComparison.OrdinalIgnoreCase)
                ? "equation.referenceBookmark.missing"
                : target.Contains("table", StringComparison.OrdinalIgnoreCase)
                    ? "table.captionBookmark.missing"
                    : "fields.ref.targetMissing";
            AddError(result, code, $"REF field target '{target}' does not exist.", "/word/document.xml", $"//w:fldSimple[contains(@w:instr,'REF {target}')]", target, "missing");
        }
    }

    private static void ValidateEquations(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("equations.omml");
        result.CheckedRules.Add("equations.numbering");
        result.CheckedRules.Add("equations.bookmarks");

        var equationParagraphs = mainPart.Document.Descendants<W.Paragraph>()
            .Where(p => p.Descendants<M.OfficeMath>().Any() || p.Descendants<M.Paragraph>().Any())
            .ToList();
        var equationBookmarks = mainPart.Document.Descendants<W.BookmarkStart>()
            .Where(b => b.Name?.Value?.Contains("eq", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        foreach (var bookmark in equationBookmarks)
        {
            var paragraph = bookmark.Ancestors<W.Paragraph>().FirstOrDefault();
            if (paragraph is not null && !paragraph.Descendants<M.OfficeMath>().Any() && !paragraph.Descendants<M.Paragraph>().Any())
            {
                AddError(result, "equation.omml.missing", $"Equation bookmark '{bookmark.Name?.Value}' is not attached to an OMML element.", "/word/document.xml", $"//w:bookmarkStart[@w:name='{bookmark.Name?.Value}']", "m:oMath or m:oMathPara", "missing");
            }
        }

        var expectedNumberPattern = EquationNumberRegex(format.Equations.Numbering.Format);
        foreach (var paragraph in equationParagraphs)
        {
            var numberTexts = paragraph.Elements<W.Run>()
                .SelectMany(run => run.Elements<W.Text>())
                .Select(text => text.Text.Trim())
                .Where(text => text.StartsWith("(", StringComparison.Ordinal) && text.EndsWith(")", StringComparison.Ordinal))
                .ToList();

            if (numberTexts.Count > 0 && !paragraph.Descendants<W.BookmarkStart>().Any())
            {
                AddError(result, "equation.bookmark.missing", "Numbered equation is missing a bookmark target.", "/word/document.xml", "//w:p[.//m:oMath and .//w:t[starts-with(.,'(')]]", "bookmark", "missing");
            }

            foreach (var number in numberTexts)
            {
                if (!Regex.IsMatch(number, expectedNumberPattern, RegexOptions.CultureInvariant))
                {
                    AddError(result, "equation.numberFormat.wrong", $"Equation number '{number}' does not match format '{format.Equations.Numbering.Format}'.", "/word/document.xml", "//w:p[.//m:oMath]//w:t", format.Equations.Numbering.Format, number);
                }
            }
        }
    }

    private static void ValidateTables(MainDocumentPart mainPart, ThesisFormatSpec format, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("tables.threeLine");
        result.CheckedRules.Add("tables.advancedGrid");
        result.CheckedRules.Add("tables.repeatHeaderRows");
        result.CheckedRules.Add("tables.rowBreak");
        result.CheckedRules.Add("tables.widthAndLayout");
        result.CheckedRules.Add("tables.cellMargins");
        foreach (var table in mainPart.Document.Descendants<W.Table>())
        {
            var tableProperties = table.GetFirstChild<W.TableProperties>();
            var borders = tableProperties?.GetFirstChild<W.TableBorders>();
            if (borders is null)
            {
                AddError(result, "table.borders.missing", "Table is missing borders.", "/word/document.xml", "//w:tbl/w:tblPr/w:tblBorders", "table borders", "missing");
                continue;
            }

            var isRenderedDataTable = tableProperties?.GetFirstChild<W.TableLayout>() is not null
                || table.Descendants<W.GridSpan>().Any()
                || table.Descendants<W.VerticalMerge>().Any()
                || table.Descendants<W.TableHeader>().Any()
                || table.Descendants<W.CantSplit>().Any();
            if (!isRenderedDataTable)
            {
                continue;
            }

            var looksLikeThreeLine = borders.BottomBorder?.Val?.Value == W.BorderValues.Single
                && borders.InsideHorizontalBorder?.Val?.Value == W.BorderValues.Single
                && borders.LeftBorder?.Val?.Value == W.BorderValues.Nil
                && borders.RightBorder?.Val?.Value == W.BorderValues.Nil
                && borders.InsideVerticalBorder?.Val?.Value == W.BorderValues.Nil;
            if (looksLikeThreeLine)
            {
                CompareBorder(result, "table.threeLine.top", "/word/document.xml", "//w:tblBorders/w:top/@w:val", borders.TopBorder?.Val?.Value);
                CompareBorder(result, "table.threeLine.bottom", "/word/document.xml", "//w:tblBorders/w:bottom/@w:val", borders.BottomBorder?.Val?.Value);
                CompareBorder(result, "table.threeLine.insideH", "/word/document.xml", "//w:tblBorders/w:insideH/@w:val", borders.InsideHorizontalBorder?.Val?.Value);
            }

            if (tableProperties?.GetFirstChild<W.TableWidth>() is null)
            {
                AddError(result, "table.width.missing", "Table width is missing.", "/word/document.xml", "//w:tbl/w:tblPr/w:tblW", "w:tblW", "missing");
            }

            if (tableProperties?.GetFirstChild<W.TableLayout>() is null)
            {
                AddError(result, "table.layout.missing", "Table layout is missing.", "/word/document.xml", "//w:tbl/w:tblPr/w:tblLayout", "w:tblLayout", "missing");
            }

            if (tableProperties?.GetFirstChild<W.TableCellMarginDefault>() is null)
            {
                AddError(result, "table.cellMargins.missing", "Default table cell margins are missing.", "/word/document.xml", "//w:tbl/w:tblPr/w:tblCellMar", "w:tblCellMar", "missing");
            }

            if (format.Tables.RepeatHeaderRowsDefault > 0 && table.Elements<W.TableRow>().Count() > format.Tables.RepeatHeaderRowsDefault)
            {
                var expectedHeaderRows = table.Elements<W.TableRow>().Take(format.Tables.RepeatHeaderRowsDefault).ToList();
                if (expectedHeaderRows.Any(row => row.GetFirstChild<W.TableRowProperties>()?.GetFirstChild<W.TableHeader>() is null))
                {
                    AddError(result, "table.repeatHeader.missing", "Expected repeated header row is missing w:tblHeader.", "/word/document.xml", "//w:tr/w:trPr/w:tblHeader", "w:tblHeader", "missing");
                }
            }

            if (!format.Tables.AllowRowBreakAcrossPagesDefault && table.Elements<W.TableRow>().Any(row => row.GetFirstChild<W.TableRowProperties>()?.GetFirstChild<W.CantSplit>() is null))
            {
                AddError(result, "table.cantSplit.missing", "A table row allows page breaks while the format requires cantSplit.", "/word/document.xml", "//w:tr/w:trPr/w:cantSplit", "w:cantSplit", "missing");
            }

            if (table.Descendants<W.VerticalMerge>().Any())
            {
                if (!table.Descendants<W.GridSpan>().Any())
                {
                    AddError(result, "table.gridSpan.missing", "Advanced table contains vertical merge but no gridSpan marker remains.", "/word/document.xml", "//w:tcPr/w:gridSpan", "w:gridSpan", "missing");
                }

                var hasRestart = table.Descendants<W.VerticalMerge>().Any(v => v.Val?.Value == W.MergedCellValues.Restart);
                if (!hasRestart)
                {
                    AddError(result, "table.verticalMerge.restartMissing", "Vertical merge exists without a restart cell.", "/word/document.xml", "//w:vMerge", "restart", "continue only");
                }
            }
        }
    }

    private static void ValidateFigures(MainDocumentPart mainPart, OpenXmlValidationResult result)
    {
        result.CheckedRules.Add("figures.drawingSize");
        foreach (var drawing in mainPart.Document.Descendants<W.Drawing>())
        {
            var extent = drawing.Descendants<WP.Extent>().FirstOrDefault();
            if (extent?.Cx is null || extent.Cy is null)
            {
                AddError(result, "figure.drawingSize.missing", "Figure drawing is missing extent size.", "/word/document.xml", "//wp:inline/wp:extent", "cx/cy", "missing");
            }
        }
    }

    private static W.NumberFormatValues ToNumberFormat(PageNumberStyle style)
    {
        return style switch
        {
            PageNumberStyle.LowerRoman => W.NumberFormatValues.LowerRoman,
            PageNumberStyle.UpperRoman => W.NumberFormatValues.UpperRoman,
            _ => W.NumberFormatValues.Decimal
        };
    }

    private static W.NumberFormatValues ToNoteNumberFormat(NoteNumberFormat format)
    {
        return format switch
        {
            NoteNumberFormat.DecimalEnclosedCircle => W.NumberFormatValues.DecimalEnclosedCircle,
            NoteNumberFormat.DecimalEnclosedCircleChinese => W.NumberFormatValues.DecimalEnclosedCircleChinese,
            _ => W.NumberFormatValues.Decimal
        };
    }

    private static W.RestartNumberValues ToNoteRestart(NoteNumberingRestart restart)
    {
        return restart switch
        {
            NoteNumberingRestart.EachPage => W.RestartNumberValues.EachPage,
            NoteNumberingRestart.EachSection => W.RestartNumberValues.EachSection,
            _ => W.RestartNumberValues.Continuous
        };
    }

    private static void Compare(OpenXmlValidationResult result, string code, string partName, string path, string? expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            AddError(result, code, $"Expected '{expected}' but found '{actual ?? "missing"}'.", partName, path, expected, actual);
        }
    }

    private static void CompareBorder(OpenXmlValidationResult result, string code, string partName, string path, W.BorderValues? actual)
    {
        if (actual != W.BorderValues.Single)
        {
            AddError(result, code, "Expected border value 'single'.", partName, path, "single", actual?.ToString());
        }
    }

    private static string? ExtractRefTarget(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return null;
        }

        var match = Regex.Match(instruction, @"^\s*REF\s+(?<target>[^\s\\]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["target"].Value : null;
    }

    private static string EquationNumberRegex(string format)
    {
        return "^" + Regex.Escape(format)
            .Replace("\\{chapter}", "\\d+", StringComparison.Ordinal)
            .Replace("\\{index}", "\\d+", StringComparison.Ordinal) + "$";
    }

    private static void AddError(OpenXmlValidationResult result, string code, string message, string? partName, string? path, string? expected, string? actual)
    {
        result.Errors.Add(new ValidationIssue
        {
            Code = code,
            Message = message,
            PartName = partName,
            Path = path,
            Expected = expected,
            Actual = actual
        });
    }
}
