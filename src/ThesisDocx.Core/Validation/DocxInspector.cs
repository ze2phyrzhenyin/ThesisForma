using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.OpenXml;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Validation;

public sealed class DocxInspector
{
    public DocxInspectionResult Inspect(string docxPath)
    {
        using var archive = ZipFile.OpenRead(docxPath);
        using var document = WordprocessingDocument.Open(docxPath, false);
        var mainPart = document.MainDocumentPart ?? throw new InvalidOperationException("Missing main document part.");
        var entries = archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToList();
        var fieldCodes = CollectFieldCodes(document);

        return new DocxInspectionResult
        {
            Entries = entries,
            PackageParts = entries,
            Styles = mainPart.StyleDefinitionsPart?.Styles?.Elements<W.Style>()
                .Select(s => s.StyleId?.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Order(StringComparer.Ordinal)
                .Cast<string>()
                .ToList() ?? [],
            StylesSummary = CollectStyles(mainPart),
            NumberingLevelTexts = mainPart.NumberingDefinitionsPart?.Numbering?.Descendants<W.LevelText>()
                .Select(t => t.Val?.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Cast<string>()
                .ToList() ?? [],
            NumberingSummary = CollectNumbering(mainPart),
            FieldCodes = fieldCodes,
            TocFields = fieldCodes.Where(f => f.Contains("TOC", StringComparison.OrdinalIgnoreCase)).ToList(),
            RefFields = fieldCodes.Where(f => f.TrimStart().StartsWith("REF", StringComparison.OrdinalIgnoreCase)).ToList(),
            SectionsSummary = CollectSections(mainPart),
            HeadersFootersSummary = CollectHeadersFooters(mainPart),
            SectionPageNumberFormats = mainPart.Document.Body?.Descendants<W.SectionProperties>()
                .Select(sp => ToPageNumberFormat(sp.GetFirstChild<W.PageNumberType>()))
                .ToList() ?? [],
            Bookmarks = mainPart.Document.Descendants<W.BookmarkStart>()
                .Select(b => b.Name?.Value)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Cast<string>()
                .Order(StringComparer.Ordinal)
                .ToList(),
            Footnotes = CollectFootnotes(mainPart),
            Endnotes = CollectEndnotes(mainPart),
            Bibliography = CollectBibliography(mainPart),
            Equations = CollectEquations(mainPart, fieldCodes),
            Tables = CollectTables(mainPart),
            TemplateRendering = CollectTemplateRendering(document),
            OpenXmlValidatorErrorCount = new OpenXmlPackageValidator().Validate(docxPath).Errors.Count,
            ParagraphCount = mainPart.Document.Body?.Descendants<W.Paragraph>().Count() ?? 0,
            TableCount = mainPart.Document.Body?.Descendants<W.Table>().Count() ?? 0,
            DrawingCount = mainPart.Document.Body?.Descendants<W.Drawing>().Count() ?? 0,
            FiguresCount = mainPart.Document.Body?.Descendants<W.Drawing>().Count() ?? 0,
            HeaderCount = mainPart.HeaderParts.Count(),
            FooterCount = mainPart.FooterParts.Count()
        };
    }

    private static List<StyleSummary> CollectStyles(MainDocumentPart mainPart)
    {
        return mainPart.StyleDefinitionsPart?.Styles?.Elements<W.Style>()
            .Select(style => new StyleSummary
            {
                StyleId = style.StyleId?.Value ?? string.Empty,
                Type = style.Type?.Value.ToString(),
                BasedOn = style.BasedOn?.Val?.Value
            })
            .OrderBy(s => s.StyleId, StringComparer.Ordinal)
            .ToList() ?? [];
    }

    private static TemplateRenderingInspectionSummary CollectTemplateRendering(WordprocessingDocument document)
    {
        var properties = document.CustomFilePropertiesPart?.Properties?.Elements<CustomDocumentProperty>()
            .Where(property => property.Name?.Value?.StartsWith("ThesisDocx.", StringComparison.Ordinal) == true)
            .ToDictionary(property => property.Name!.Value!, property => property.VTLPWSTR?.Text ?? string.Empty, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var text = string.Concat(document.MainDocumentPart?.Document.Descendants<W.Text>().Select(t => t.Text) ?? []);

        return new TemplateRenderingInspectionSummary
        {
            TemplateId = properties.GetValueOrDefault("ThesisDocx.TemplateId"),
            TemplateVersion = properties.GetValueOrDefault("ThesisDocx.TemplateVersion"),
            RendererVersion = properties.GetValueOrDefault("ThesisDocx.RendererVersion"),
            ResolvedFormatSpecVersion = properties.GetValueOrDefault("ThesisDocx.ResolvedFormatSpecVersion"),
            RenderedPageTemplates = SplitCsv(properties.GetValueOrDefault("ThesisDocx.RenderedPageTemplates")),
            RenderedVariables = SplitCsv(properties.GetValueOrDefault("ThesisDocx.RenderedVariables")),
            RenderedAssets = SplitCsv(properties.GetValueOrDefault("ThesisDocx.RenderedAssets")),
            CoverSummary = new CoverInspectionSummary
            {
                HasTitle = text.Contains("结构化毕业论文 DOCX 渲染引擎", StringComparison.Ordinal) || text.Contains("Thesis", StringComparison.OrdinalIgnoreCase),
                HasMetadataFieldTable = HasCoverMetadataTable(document),
                HasLogoDrawing = document.MainDocumentPart?.Document.Descendants<W.Drawing>().Any() == true
            },
            DeclarationSummary = new DeclarationInspectionSummary
            {
                HasDeclarationText = HasDeclarationText(text),
                HasSignatureField = text.Contains("签名", StringComparison.Ordinal) || text.Contains("Signature", StringComparison.OrdinalIgnoreCase)
            },
            RuleParagraphCount = document.MainDocumentPart?.Document.Descendants<W.Paragraph>().Count(HasBottomRule) ?? 0
        };
    }

    private static bool HasCoverMetadataTable(WordprocessingDocument document)
    {
        return document.MainDocumentPart?.Document.Descendants<W.Table>()
            .Any(table =>
            {
                var tableText = string.Concat(table.Descendants<W.Text>().Select(t => t.Text));
                return tableText.Contains("学号", StringComparison.Ordinal)
                    && (tableText.Contains("作者", StringComparison.Ordinal) || tableText.Contains("姓名", StringComparison.Ordinal))
                    && (tableText.Contains("导师", StringComparison.Ordinal) || tableText.Contains("Advisor", StringComparison.OrdinalIgnoreCase));
            }) == true;
    }

    private static bool HasDeclarationText(string text)
    {
        return text.Contains("declaration", StringComparison.OrdinalIgnoreCase)
            || (text.Contains("声明", StringComparison.Ordinal)
                && (text.Contains("独立完成", StringComparison.Ordinal)
                    || text.Contains("学术规范", StringComparison.Ordinal)
                    || text.Contains("原创", StringComparison.Ordinal)));
    }

    private static List<string> SplitCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    private static bool HasBottomRule(W.Paragraph paragraph)
    {
        return paragraph.ParagraphProperties?.ParagraphBorders?.BottomBorder?.Val is not null;
    }

    private static List<NumberingSummary> CollectNumbering(MainDocumentPart mainPart)
    {
        return mainPart.NumberingDefinitionsPart?.Numbering?.Elements<W.AbstractNum>()
            .Select(abstractNum => new NumberingSummary
            {
                AbstractNumberId = abstractNum.AbstractNumberId?.Value,
                LevelTexts = abstractNum.Elements<W.Level>()
                    .SelectMany(level => level.Elements<W.LevelText>())
                    .Select(text => text.Val?.Value)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Cast<string>()
                    .ToList()
            })
            .ToList() ?? [];
    }

    private static List<SectionSummary> CollectSections(MainDocumentPart mainPart)
    {
        return mainPart.Document.Body?.Descendants<W.SectionProperties>()
            .Select((section, index) =>
            {
                var pageNumber = section.GetFirstChild<W.PageNumberType>();
                var pageSize = section.GetFirstChild<W.PageSize>();
                var margin = section.GetFirstChild<W.PageMargin>();
                return new SectionSummary
                {
                    Index = index,
                    PageNumberFormat = ToPageNumberFormat(pageNumber),
                    PageNumberStart = pageNumber?.Start?.Value,
                    WidthTwips = pageSize?.Width?.Value.ToString(),
                    HeightTwips = pageSize?.Height?.Value.ToString(),
                    TopMarginTwips = margin?.Top?.Value.ToString(),
                    BottomMarginTwips = margin?.Bottom?.Value.ToString(),
                    LeftMarginTwips = margin?.Left?.Value.ToString(),
                    RightMarginTwips = margin?.Right?.Value.ToString()
                };
            })
            .ToList() ?? [];
    }

    private static List<HeaderFooterSummary> CollectHeadersFooters(MainDocumentPart mainPart)
    {
        var summaries = new List<HeaderFooterSummary>();
        summaries.AddRange(mainPart.HeaderParts.Select(part => new HeaderFooterSummary
        {
            Kind = "header",
            RelationshipId = mainPart.GetIdOfPart(part),
            HasHeaderLine = part.Header.Descendants<W.ParagraphBorders>().Any()
        }));
        summaries.AddRange(mainPart.FooterParts.Select(part => new HeaderFooterSummary
        {
            Kind = "footer",
            RelationshipId = mainPart.GetIdOfPart(part),
            HasPageField = part.Footer.Descendants<W.SimpleField>().Any(field => field.Instruction?.Value?.Contains("PAGE", StringComparison.OrdinalIgnoreCase) == true)
        }));
        return summaries.OrderBy(s => s.Kind, StringComparer.Ordinal).ThenBy(s => s.RelationshipId, StringComparer.Ordinal).ToList();
    }

    private static NoteInspectionSummary CollectFootnotes(MainDocumentPart mainPart)
    {
        var notes = mainPart.FootnotesPart?.Footnotes?.Elements<W.Footnote>().ToList() ?? [];
        var ids = notes.Select(note => note.Id?.Value).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        return new NoteInspectionSummary
        {
            HasPart = mainPart.FootnotesPart is not null,
            Count = ids.Count(id => id > 0),
            Ids = ids.Where(id => id > 0).Order().ToList(),
            StyleIds = NoteStyleIds(notes),
            ReferenceMarkCount = notes.SelectMany(note => note.Descendants<W.FootnoteReferenceMark>()).Count(),
            HasSeparator = ids.Contains(-1),
            HasContinuationSeparator = ids.Contains(0)
        };
    }

    private static NoteInspectionSummary CollectEndnotes(MainDocumentPart mainPart)
    {
        var notes = mainPart.EndnotesPart?.Endnotes?.Elements<W.Endnote>().ToList() ?? [];
        var ids = notes.Select(note => note.Id?.Value).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        return new NoteInspectionSummary
        {
            HasPart = mainPart.EndnotesPart is not null,
            Count = ids.Count(id => id > 0),
            Ids = ids.Where(id => id > 0).Order().ToList(),
            StyleIds = NoteStyleIds(notes),
            ReferenceMarkCount = notes.SelectMany(note => note.Descendants<W.EndnoteReferenceMark>()).Count(),
            HasSeparator = ids.Contains(-1),
            HasContinuationSeparator = ids.Contains(0)
        };
    }

    private static List<string> NoteStyleIds(IEnumerable<OpenXmlElement> notes)
    {
        return notes
            .Where(note => note.GetAttribute("id", "http://schemas.openxmlformats.org/wordprocessingml/2006/main").Value is not "-1" and not "0")
            .SelectMany(note => note.Elements<W.Paragraph>())
            .Select(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static BibliographyInspectionSummary CollectBibliography(MainDocumentPart mainPart)
    {
        var paragraphs = mainPart.Document.Descendants<W.Paragraph>()
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == StyleIds.Bibliography)
            .ToList();
        return new BibliographyInspectionSummary
        {
            EntryCount = paragraphs.Count,
            HangingIndents = paragraphs
                .Select(p => p.ParagraphProperties?.Indentation?.Hanging?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }

    private static EquationInspectionSummary CollectEquations(MainDocumentPart mainPart, IReadOnlyList<string> fieldCodes)
    {
        var equationParagraphs = mainPart.Document.Descendants<W.Paragraph>()
            .Where(p => p.Descendants<M.OfficeMath>().Any() || p.Descendants<M.Paragraph>().Any())
            .ToList();
        var bookmarks = equationParagraphs
            .SelectMany(p => p.Descendants<W.BookmarkStart>())
            .Select(b => b.Name?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var sourceTypes = equationParagraphs
            .Select(InferEquationSourceType)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new EquationInspectionSummary
        {
            Count = equationParagraphs.Count,
            Ids = bookmarks,
            SourceTypes = sourceTypes,
            HasNumbering = equationParagraphs.Any(HasEquationNumber),
            Bookmarks = bookmarks,
            OmmlElementCount = mainPart.Document.Descendants<M.OfficeMath>().Count() + mainPart.Document.Descendants<M.Paragraph>().Count(),
            RefFields = fieldCodes.Where(f => f.Contains("bm-eq-", StringComparison.Ordinal) || f.Contains("eq-", StringComparison.Ordinal)).ToList()
        };
    }

    private static TableInspectionSummary CollectTables(MainDocumentPart mainPart)
    {
        var tables = mainPart.Document.Descendants<W.Table>().ToList();
        var captionParagraphs = mainPart.Document.Descendants<W.Paragraph>()
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == StyleIds.Caption)
            .Select(p => string.Concat(p.Descendants<W.Text>().Select(t => t.Text)))
            .Where(text => text.StartsWith("表", StringComparison.Ordinal))
            .ToList();
        var tableBookmarks = tables
            .SelectMany(t => t.Descendants<W.BookmarkStart>())
            .Select(b => b.Name?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new TableInspectionSummary
        {
            Count = tables.Count,
            Captions = captionParagraphs,
            Bookmarks = tableBookmarks,
            Styles = tables.Select(t => InferTableStyle(t)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            HasGridSpan = tables.Any(t => t.Descendants<W.GridSpan>().Any()),
            HasVerticalMerge = tables.Any(t => t.Descendants<W.VerticalMerge>().Any()),
            HasRepeatHeaderRows = tables.Any(t => t.Descendants<W.TableHeader>().Any()),
            HasCantSplitRows = tables.Any(t => t.Descendants<W.CantSplit>().Any()),
            HasNestedCellBlocks = tables.SelectMany(t => t.Descendants<W.TableCell>()).Any(cell => cell.Elements<W.Paragraph>().Count() > 1),
            HasCellNoteReferences = tables.Any(t => t.Descendants<W.FootnoteReference>().Any() || t.Descendants<W.EndnoteReference>().Any()),
            CellParagraphStyleIds = tables
                .SelectMany(t => t.Descendants<W.TableCell>())
                .SelectMany(cell => cell.Elements<W.Paragraph>())
                .Select(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList(),
            WidthTypes = tables
                .Select(t => ToTableWidthType(t.GetFirstChild<W.TableProperties>()?.GetFirstChild<W.TableWidth>()?.Type?.Value))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList(),
            BorderSummary = tables.Select(SummarizeBorders).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()
        };
    }

    private static string InferEquationSourceType(W.Paragraph paragraph)
    {
        if (paragraph.Descendants<M.Superscript>().Any() || paragraph.Descendants<M.Subscript>().Any())
        {
            return "latexSubsetOrStructuredOmml";
        }

        return "plainOrOmml";
    }

    private static bool HasEquationNumber(W.Paragraph paragraph)
    {
        return paragraph.Elements<W.Run>()
            .SelectMany(run => run.Elements<W.Text>())
            .Any(text => text.Text.Trim().StartsWith("(", StringComparison.Ordinal) && text.Text.Trim().EndsWith(")", StringComparison.Ordinal));
    }

    private static string InferTableStyle(W.Table table)
    {
        var borders = table.GetFirstChild<W.TableProperties>()?.GetFirstChild<W.TableBorders>();
        if (borders?.LeftBorder?.Val?.Value == W.BorderValues.Nil
            && borders.RightBorder?.Val?.Value == W.BorderValues.Nil
            && borders.InsideVerticalBorder?.Val?.Value == W.BorderValues.Nil)
        {
            return "threeLine";
        }

        return "normal";
    }

    private static string SummarizeBorders(W.Table table)
    {
        var borders = table.GetFirstChild<W.TableProperties>()?.GetFirstChild<W.TableBorders>();
        if (borders is null)
        {
            return "missing";
        }

        return string.Join("|", new[]
        {
            $"top:{ToBorderName(borders.TopBorder?.Val?.Value)}",
            $"bottom:{ToBorderName(borders.BottomBorder?.Val?.Value)}",
            $"left:{ToBorderName(borders.LeftBorder?.Val?.Value)}",
            $"right:{ToBorderName(borders.RightBorder?.Val?.Value)}",
            $"insideH:{ToBorderName(borders.InsideHorizontalBorder?.Val?.Value)}",
            $"insideV:{ToBorderName(borders.InsideVerticalBorder?.Val?.Value)}"
        });
    }

    private static string ToTableWidthType(W.TableWidthUnitValues? value)
    {
        if (value is null)
        {
            return "missing";
        }

        if (value == W.TableWidthUnitValues.Pct)
        {
            return "pct";
        }

        if (value == W.TableWidthUnitValues.Dxa)
        {
            return "dxa";
        }

        if (value == W.TableWidthUnitValues.Auto)
        {
            return "auto";
        }

        return value.Value.ToString();
    }

    private static string ToBorderName(W.BorderValues? value)
    {
        if (value is null)
        {
            return "missing";
        }

        if (value == W.BorderValues.Nil)
        {
            return "nil";
        }

        if (value == W.BorderValues.None)
        {
            return "none";
        }

        if (value == W.BorderValues.Single)
        {
            return "single";
        }

        if (value == W.BorderValues.Double)
        {
            return "double";
        }

        if (value == W.BorderValues.Dotted)
        {
            return "dotted";
        }

        if (value == W.BorderValues.Dashed)
        {
            return "dashed";
        }

        return value.Value.ToString();
    }

    private static List<string> CollectFieldCodes(WordprocessingDocument document)
    {
        var fields = new List<string>();
        var mainPart = document.MainDocumentPart;
        if (mainPart is null)
        {
            return fields;
        }

        fields.AddRange(mainPart.Document.Descendants<W.SimpleField>()
            .Select(f => f.Instruction?.Value)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Cast<string>());

        foreach (var footer in mainPart.FooterParts)
        {
            fields.AddRange(footer.Footer.Descendants<W.SimpleField>()
                .Select(f => f.Instruction?.Value)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Cast<string>());
        }

        return fields.Order(StringComparer.Ordinal).ToList();
    }

    private static string ToPageNumberFormat(W.PageNumberType? pageNumberType)
    {
        if (pageNumberType?.Format is null)
        {
            return "none";
        }

        var value = pageNumberType.Format.Value;
        if (value == W.NumberFormatValues.LowerRoman)
        {
            return "lowerRoman";
        }

        if (value == W.NumberFormatValues.UpperRoman)
        {
            return "upperRoman";
        }

        return "decimal";
    }
}
