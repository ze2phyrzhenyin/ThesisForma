using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Utilities;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class BodyRenderer
{
    private readonly ThesisMetadata _metadata;
    private readonly RelationshipManager _relationshipManager;
    private readonly NoteManager _noteManager;
    private readonly ParagraphRenderer _paragraphRenderer;
    private readonly HeadingRenderer _headingRenderer;
    private readonly FieldCodeRenderer _fieldCodeRenderer;
    private readonly TableRenderer _tableRenderer;
    private readonly FigureRenderer _figureRenderer;
    private readonly BibliographyRenderer _bibliographyRenderer;
    private readonly EquationRenderer _equationRenderer;
    private readonly PreservedObjectRenderer _preservedObjectRenderer;
    private readonly ThesisDocument _document;
    private readonly ThesisFormatSpec _format;
    private readonly DocxRenderContext? _context;
    private readonly PageTemplateRenderer _pageTemplateRenderer;
    private SectionInstanceOverrideSpec? _currentSectionOverride;

    public BodyRenderer(
        MainDocumentPart mainPart,
        RelationshipManager relationshipManager,
        ThesisFormatSpec format,
        ThesisMetadata metadata,
        ThesisDocument document,
        DocxRenderContext? context = null)
    {
        _metadata = metadata;
        _document = document;
        _format = format;
        _context = context;
        _relationshipManager = relationshipManager;
        _noteManager = new NoteManager(mainPart, format.Notes);
        var captionRenderer = new CaptionRenderer(format);
        _paragraphRenderer = new ParagraphRenderer(relationshipManager, _noteManager);
        _headingRenderer = new HeadingRenderer(relationshipManager, _paragraphRenderer);
        _fieldCodeRenderer = new FieldCodeRenderer(format, context?.Overrides);
        _tableRenderer = new TableRenderer(format, captionRenderer, relationshipManager, _paragraphRenderer);
        _figureRenderer = new FigureRenderer(relationshipManager, format, captionRenderer);
        _bibliographyRenderer = new BibliographyRenderer(format);
        _equationRenderer = new EquationRenderer(format, relationshipManager);
        _preservedObjectRenderer = new PreservedObjectRenderer(_paragraphRenderer, relationshipManager);
        _pageTemplateRenderer = new PageTemplateRenderer(relationshipManager);
    }

    public void SaveNoteParts()
    {
        _noteManager.SaveParts();
    }

    public void RenderSection(W.Body body, ThesisSection section)
    {
        _currentSectionOverride = FindSectionOverride(section);
        var pageTemplates = FindPageTemplates(section.Kind, section.Id).ToList();
        foreach (var pageTemplate in pageTemplates.Where(layout => layout.InsertPosition == PageTemplateInsertPosition.BeforeSection))
        {
            RenderPageTemplate(body, pageTemplate);
        }

        var replacementTemplates = pageTemplates.Where(layout => layout.InsertPosition == PageTemplateInsertPosition.ReplaceSectionContent).ToList();
        if (replacementTemplates.Count > 0)
        {
            foreach (var pageTemplate in replacementTemplates)
            {
                RenderPageTemplate(body, pageTemplate);
            }

            return;
        }

        if (section.Kind == ThesisSectionKind.Cover && section.Blocks.Count == 0)
        {
            RenderDefaultCover(body);
        }
        else if (section.Kind == ThesisSectionKind.Toc)
        {
            foreach (var paragraph in _fieldCodeRenderer.CreateToc())
            {
                body.AppendChild(ApplyCurrentSectionOverrides(paragraph));
            }
        }
        else if ((section.Kind is ThesisSectionKind.Abstract or ThesisSectionKind.OriginalityStatement) && !string.IsNullOrWhiteSpace(section.Title))
        {
            body.AppendChild(ApplyTitleOverrides(_paragraphRenderer.CreatePlainParagraph(section.Title!, StyleIds.TocTitle, TextAlignment.Center)));
        }
        else if (section.Kind == ThesisSectionKind.Bibliography && !string.IsNullOrWhiteSpace(section.Title))
        {
            body.AppendChild(ApplyTitleOverrides(_headingRenderer.CreateHeading(new HeadingBlock
            {
                Level = 1,
                Numbered = false,
                Inlines = [new TextInline { Text = section.Title! }]
            })));
        }

        for (var blockIndex = 0; blockIndex < section.Blocks.Count; blockIndex++)
        {
            foreach (var element in RenderBlock(section.Blocks[blockIndex], blockIndex))
            {
                body.AppendChild(element);
            }
        }

        foreach (var pageTemplate in pageTemplates.Where(layout => layout.InsertPosition == PageTemplateInsertPosition.AfterSection))
        {
            RenderPageTemplate(body, pageTemplate);
        }
    }

    private IEnumerable<TemplatePageLayout> FindPageTemplates(ThesisSectionKind kind, string? sectionId)
    {
        if (_context is null)
        {
            yield break;
        }

        var target = kind switch
        {
            ThesisSectionKind.Cover => PageTemplateTargetSectionType.Cover,
            ThesisSectionKind.OriginalityStatement => PageTemplateTargetSectionType.Declaration,
            ThesisSectionKind.Abstract => PageTemplateTargetSectionType.Abstract,
            ThesisSectionKind.Toc => PageTemplateTargetSectionType.Toc,
            ThesisSectionKind.Appendix => PageTemplateTargetSectionType.Appendix,
            ThesisSectionKind.Acknowledgements => PageTemplateTargetSectionType.Acknowledgements,
            ThesisSectionKind.Bibliography => PageTemplateTargetSectionType.Bibliography,
            ThesisSectionKind.TeacherComments => PageTemplateTargetSectionType.TeacherComments,
            _ => PageTemplateTargetSectionType.Body
        };
        foreach (var layout in _context.PageTemplates
            .Where(layout => layout.TargetSectionType == target)
            .Where(layout => string.IsNullOrWhiteSpace(layout.TargetSectionId)
                || string.Equals(layout.TargetSectionId, sectionId, StringComparison.Ordinal))
            .OrderBy(layout => string.IsNullOrWhiteSpace(layout.TargetSectionId) ? 1 : 0)
            .ThenBy(layout => layout.Id, StringComparer.Ordinal))
        {
            yield return layout;
        }
    }

    private void RenderPageTemplate(W.Body body, TemplatePageLayout layout)
    {
        if (_context is null)
        {
            return;
        }

        var template = new TemplatePackage
        {
            Id = _context.TemplateId ?? string.Empty,
            Version = _context.TemplateVersion ?? string.Empty,
            School = _context.TemplateSchool ?? string.Empty,
            College = _context.TemplateCollege ?? string.Empty
        };
        foreach (var element in _pageTemplateRenderer.Render(layout, _document, new TemplatePackageShim(template), _context))
        {
            body.AppendChild(element);
        }
    }

    private IEnumerable<OpenXmlElement> RenderBlock(BlockNode block, int blockIndex)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                yield return ApplyBlockOverrides(ApplyCurrentSectionOverrides(_paragraphRenderer.CreateParagraph(paragraph)), blockIndex);
                break;
            case HeadingBlock heading:
                _equationRenderer.NotifyHeading(heading);
                yield return ApplyBlockOverrides(ApplyCurrentSectionOverrides(_headingRenderer.CreateHeading(heading)), blockIndex);
                break;
            case ListBlock list:
                foreach (var paragraph in RenderList(list))
                {
                    yield return paragraph;
                }

                break;
            case FigureBlock figure:
                foreach (var element in _figureRenderer.Render(figure))
                {
                    yield return element;
                }

                break;
            case TableBlock table:
                foreach (var element in _tableRenderer.Render(table))
                {
                    yield return element;
                }

                break;
            case QuoteBlock quote:
                yield return ApplyCurrentSectionOverrides(_paragraphRenderer.CreateParagraph(new ParagraphBlock
                {
                    StyleId = StyleIds.Quote,
                    Inlines = quote.Inlines
                }));
                break;
            case EquationBlock equation:
                foreach (var element in _equationRenderer.Render(equation))
                {
                    yield return element;
                }

                break;
            case PreservedObjectBlock preserved:
                foreach (var element in _preservedObjectRenderer.Render(preserved))
                {
                    yield return element;
                }

                break;
            case PageBreakBlock:
                yield return new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page }));
                break;
            case SectionBreakBlock:
                yield return new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page }));
                break;
            case BibliographyBlock bibliography:
                foreach (var paragraph in _bibliographyRenderer.Render(bibliography))
                {
                    yield return paragraph;
                }

                break;
            case FootnoteBlock footnote:
                yield return ApplyCurrentSectionOverrides(new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody }),
                    _noteManager.CreateFootnoteReference(footnote.NoteId, footnote.Inlines)));
                break;
            case EndnoteBlock endnote:
                yield return ApplyCurrentSectionOverrides(new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody }),
                    _noteManager.CreateEndnoteReference(endnote.NoteId, endnote.Inlines)));
                break;
        }
    }

    private IEnumerable<W.Paragraph> RenderList(ListBlock list)
    {
        foreach (var item in list.Items)
        {
            var firstParagraph = item.Blocks.OfType<ParagraphBlock>().FirstOrDefault();
            if (firstParagraph is null)
            {
                continue;
            }

            var paragraph = _paragraphRenderer.CreateParagraph(firstParagraph);
            paragraph.ParagraphProperties ??= new W.ParagraphProperties();
            if (list.Ordered)
            {
                paragraph.ParagraphProperties.AppendChild(new W.NumberingProperties(
                    new W.NumberingLevelReference { Val = 0 },
                    new W.NumberingId { Val = NumberingBuilder.OrderedListNumberingId }));
            }
            else
            {
                if (paragraph.ParagraphProperties is not null)
                {
                    paragraph.InsertAfter(new W.Run(new W.Text("• ")), paragraph.ParagraphProperties);
                }
                else
                {
                    paragraph.PrependChild(new W.Run(new W.Text("• ")));
                }
            }

            yield return ApplyCurrentSectionOverrides(paragraph);
        }
    }

    private void RenderDefaultCover(W.Body body)
    {
        body.AppendChild(ApplyCurrentSectionOverrides(_paragraphRenderer.CreatePlainParagraph(_metadata.Title, StyleIds.TocTitle, TextAlignment.Center)));
        body.AppendChild(ApplyCurrentSectionOverrides(_paragraphRenderer.CreatePlainParagraph($"作者：{_metadata.Author}", StyleIds.ThesisBody, TextAlignment.Center)));
        body.AppendChild(ApplyCurrentSectionOverrides(_paragraphRenderer.CreatePlainParagraph($"学号：{_metadata.StudentId}", StyleIds.ThesisBody, TextAlignment.Center)));
        body.AppendChild(ApplyCurrentSectionOverrides(_paragraphRenderer.CreatePlainParagraph($"学院：{_metadata.College}", StyleIds.ThesisBody, TextAlignment.Center)));
        body.AppendChild(ApplyCurrentSectionOverrides(_paragraphRenderer.CreatePlainParagraph($"专业：{_metadata.Major}", StyleIds.ThesisBody, TextAlignment.Center)));
        body.AppendChild(ApplyCurrentSectionOverrides(_paragraphRenderer.CreatePlainParagraph($"导师：{_metadata.Advisor}", StyleIds.ThesisBody, TextAlignment.Center)));
        body.AppendChild(ApplyCurrentSectionOverrides(_paragraphRenderer.CreatePlainParagraph(_metadata.Date, StyleIds.ThesisBody, TextAlignment.Center)));
    }

    private SectionInstanceOverrideSpec? FindSectionOverride(ThesisSection section)
    {
        if (section.Id is null || _context?.Overrides?.SectionInstances is null)
        {
            return null;
        }

        return _context.Overrides.SectionInstances.TryGetValue(section.Id, out var instance)
            ? instance
            : null;
    }

    private W.Paragraph ApplyCurrentSectionOverrides(W.Paragraph paragraph)
    {
        if (_currentSectionOverride is null)
        {
            return paragraph;
        }

        if (CanApplySectionDirectFormatting(paragraph))
        {
            ApplyParagraphOverride(paragraph, _currentSectionOverride.Paragraph);
            ApplyDefaultFontOverride(paragraph, _currentSectionOverride.DefaultFont);
        }

        return paragraph;
    }

    private W.Paragraph ApplyTitleOverrides(W.Paragraph paragraph)
    {
        if (_currentSectionOverride is null)
        {
            return paragraph;
        }

        ApplyParagraphOverride(paragraph, _currentSectionOverride.TitleParagraph);
        ApplyDefaultFontOverride(paragraph, _currentSectionOverride.TitleFont);
        return paragraph;
    }

    private W.Paragraph ApplyBlockOverrides(W.Paragraph paragraph, int blockIndex)
    {
        if (_currentSectionOverride?.BlockOverrides is null
            || !_currentSectionOverride.BlockOverrides.TryGetValue(blockIndex, out var blockOverride))
        {
            return paragraph;
        }

        ApplyParagraphOverride(paragraph, blockOverride.Paragraph);
        ApplyDefaultFontOverride(paragraph, blockOverride.Font);
        return paragraph;
    }

    private static bool CanApplySectionDirectFormatting(W.Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return string.IsNullOrWhiteSpace(styleId)
            || styleId is StyleIds.ThesisBody or StyleIds.Quote or StyleIds.Bibliography;
    }

    private void ApplyParagraphOverride(W.Paragraph paragraph, ParagraphOverrideSpec? overrideSpec)
    {
        if (overrideSpec is null)
        {
            return;
        }

        var merged = DocumentOverridesFormatMerger.MergeParagraph(_format.BodyParagraph, overrideSpec);
        paragraph.ParagraphProperties ??= new W.ParagraphProperties();
        var outlineLevel = (W.OutlineLevel?)paragraph.ParagraphProperties.GetFirstChild<W.OutlineLevel>()?.CloneNode(true);
        paragraph.ParagraphProperties.RemoveAllChildren<W.WidowControl>();
        paragraph.ParagraphProperties.RemoveAllChildren<W.SpacingBetweenLines>();
        paragraph.ParagraphProperties.RemoveAllChildren<W.Indentation>();
        paragraph.ParagraphProperties.RemoveAllChildren<W.Justification>();
        paragraph.ParagraphProperties.RemoveAllChildren<W.OutlineLevel>();

        paragraph.ParagraphProperties.AppendChild(StyleBuilder.CreateSpacing(merged));

        var font = DocumentOverridesFormatMerger.MergeFont(_format.DefaultFont, _currentSectionOverride?.DefaultFont);
        var indentation = new W.Indentation();
        if (merged.FirstLineIndentChars > 0)
        {
            indentation.FirstLine = UnitConverter.PointsToTwips(font.SizePt * merged.FirstLineIndentChars).ToString();
        }

        if (merged.HangingIndentCm > 0)
        {
            indentation.Hanging = UnitConverter.CentimetersToTwips(merged.HangingIndentCm).ToString();
        }

        if (indentation.HasAttributes)
        {
            paragraph.ParagraphProperties.AppendChild(indentation);
        }

        paragraph.ParagraphProperties.AppendChild(new W.Justification { Val = StyleBuilder.ToJustification(merged.Alignment) });
        if (outlineLevel is not null)
        {
            paragraph.ParagraphProperties.AppendChild(outlineLevel);
        }
    }

    private void ApplyDefaultFontOverride(W.Paragraph paragraph, FontOverrideSpec? overrideSpec)
    {
        if (overrideSpec is null)
        {
            return;
        }

        var merged = DocumentOverridesFormatMerger.MergeFont(_format.DefaultFont, overrideSpec);
        foreach (var run in paragraph.Descendants<W.Run>())
        {
            if (run.Descendants<W.FootnoteReference>().Any()
                || run.Descendants<W.EndnoteReference>().Any()
                || run.RunProperties?.GetFirstChild<W.VerticalTextAlignment>() is not null)
            {
                continue;
            }

            var runProperties = run.RunProperties;
            if (runProperties is null)
            {
                runProperties = new W.RunProperties();
                run.PrependChild(runProperties);
            }

            runProperties.RemoveAllChildren<W.RunFonts>();
            runProperties.RemoveAllChildren<W.FontSize>();
            runProperties.RemoveAllChildren<W.FontSizeComplexScript>();
            runProperties.RemoveAllChildren<W.Bold>();
            runProperties.RemoveAllChildren<W.BoldComplexScript>();
            runProperties.RemoveAllChildren<W.Italic>();
            runProperties.RemoveAllChildren<W.ItalicComplexScript>();
            runProperties.InsertAt(StyleBuilder.CreateRunFonts(merged), 0);

            if (merged.Bold)
            {
                runProperties.AppendChild(new W.Bold());
                runProperties.AppendChild(new W.BoldComplexScript());
            }

            if (merged.Italic)
            {
                runProperties.AppendChild(new W.Italic());
                runProperties.AppendChild(new W.ItalicComplexScript());
            }

            runProperties.AppendChild(new W.FontSize { Val = UnitConverter.PointsToHalfPoints(merged.SizePt).ToString() });
            runProperties.AppendChild(new W.FontSizeComplexScript { Val = UnitConverter.PointsToHalfPoints(merged.SizePt).ToString() });
        }
    }
}
