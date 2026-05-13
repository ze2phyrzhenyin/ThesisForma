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
        _pageTemplateRenderer = new PageTemplateRenderer(relationshipManager);
    }

    public void SaveNoteParts()
    {
        _noteManager.SaveParts();
    }

    public void RenderSection(W.Body body, ThesisSection section)
    {
        _currentSectionOverride = FindSectionOverride(section);
        var pageTemplate = FindPageTemplate(section.Kind);
        if (pageTemplate is not null && pageTemplate.InsertPosition == PageTemplateInsertPosition.BeforeSection)
        {
            RenderPageTemplate(body, pageTemplate);
        }

        if (pageTemplate is not null && pageTemplate.InsertPosition == PageTemplateInsertPosition.ReplaceSectionContent)
        {
            RenderPageTemplate(body, pageTemplate);
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
            body.AppendChild(ApplyCurrentSectionOverrides(_paragraphRenderer.CreatePlainParagraph(section.Title!, StyleIds.TocTitle, TextAlignment.Center)));
        }
        else if (section.Kind == ThesisSectionKind.Bibliography && !string.IsNullOrWhiteSpace(section.Title))
        {
            body.AppendChild(ApplyCurrentSectionOverrides(_headingRenderer.CreateHeading(new HeadingBlock
            {
                Level = 1,
                Numbered = false,
                Inlines = [new TextInline { Text = section.Title! }]
            })));
        }

        foreach (var block in section.Blocks)
        {
            foreach (var element in RenderBlock(block))
            {
                body.AppendChild(element);
            }
        }

        if (pageTemplate is not null && pageTemplate.InsertPosition == PageTemplateInsertPosition.AfterSection)
        {
            RenderPageTemplate(body, pageTemplate);
        }
    }

    private TemplatePageLayout? FindPageTemplate(ThesisSectionKind kind)
    {
        if (_context is null)
        {
            return null;
        }

        var target = kind switch
        {
            ThesisSectionKind.Cover => PageTemplateTargetSectionType.Cover,
            ThesisSectionKind.OriginalityStatement => PageTemplateTargetSectionType.Declaration,
            ThesisSectionKind.Abstract => PageTemplateTargetSectionType.Abstract,
            ThesisSectionKind.Toc => PageTemplateTargetSectionType.Toc,
            ThesisSectionKind.Appendix => PageTemplateTargetSectionType.Appendix,
            _ => PageTemplateTargetSectionType.Body
        };
        return _context.PageTemplates.FirstOrDefault(layout => layout.TargetSectionType == target);
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

    private IEnumerable<OpenXmlElement> RenderBlock(BlockNode block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                yield return ApplyCurrentSectionOverrides(_paragraphRenderer.CreateParagraph(paragraph));
                break;
            case HeadingBlock heading:
                _equationRenderer.NotifyHeading(heading);
                yield return ApplyCurrentSectionOverrides(_headingRenderer.CreateHeading(heading));
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
        paragraph.ParagraphProperties.RemoveAllChildren<W.WidowControl>();
        paragraph.ParagraphProperties.RemoveAllChildren<W.SpacingBetweenLines>();
        paragraph.ParagraphProperties.RemoveAllChildren<W.Indentation>();
        paragraph.ParagraphProperties.RemoveAllChildren<W.Justification>();

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
            runProperties.InsertAt(StyleBuilder.CreateRunFonts(merged), 0);
            runProperties.AppendChild(new W.FontSize { Val = UnitConverter.PointsToHalfPoints(merged.SizePt).ToString() });
            runProperties.AppendChild(new W.FontSizeComplexScript { Val = UnitConverter.PointsToHalfPoints(merged.SizePt).ToString() });

            if (overrideSpec.Bold.HasValue)
            {
                runProperties.RemoveAllChildren<W.Bold>();
                runProperties.RemoveAllChildren<W.BoldComplexScript>();
                if (overrideSpec.Bold.Value)
                {
                    runProperties.AppendChild(new W.Bold());
                    runProperties.AppendChild(new W.BoldComplexScript());
                }
            }

            if (overrideSpec.Italic.HasValue)
            {
                runProperties.RemoveAllChildren<W.Italic>();
                runProperties.RemoveAllChildren<W.ItalicComplexScript>();
                if (overrideSpec.Italic.Value)
                {
                    runProperties.AppendChild(new W.Italic());
                    runProperties.AppendChild(new W.ItalicComplexScript());
                }
            }
        }
    }
}
