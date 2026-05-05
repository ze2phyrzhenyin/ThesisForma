using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.OpenXml;
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
    private readonly DocxRenderContext? _context;
    private readonly PageTemplateRenderer _pageTemplateRenderer;

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
        _context = context;
        _relationshipManager = relationshipManager;
        _noteManager = new NoteManager(mainPart);
        var captionRenderer = new CaptionRenderer(format);
        _paragraphRenderer = new ParagraphRenderer(relationshipManager, _noteManager);
        _headingRenderer = new HeadingRenderer(relationshipManager, _paragraphRenderer);
        _fieldCodeRenderer = new FieldCodeRenderer(format);
        _tableRenderer = new TableRenderer(format, captionRenderer, relationshipManager);
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
                body.AppendChild(paragraph);
            }
        }
        else if ((section.Kind is ThesisSectionKind.Abstract or ThesisSectionKind.OriginalityStatement) && !string.IsNullOrWhiteSpace(section.Title))
        {
            body.AppendChild(_paragraphRenderer.CreatePlainParagraph(section.Title!, StyleIds.TocTitle, TextAlignment.Center));
        }
        else if (section.Kind == ThesisSectionKind.Bibliography && !string.IsNullOrWhiteSpace(section.Title))
        {
            body.AppendChild(_headingRenderer.CreateHeading(new HeadingBlock
            {
                Level = 1,
                Numbered = false,
                Inlines = [new TextInline { Text = section.Title! }]
            }));
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
                yield return _paragraphRenderer.CreateParagraph(paragraph);
                break;
            case HeadingBlock heading:
                _equationRenderer.NotifyHeading(heading);
                yield return _headingRenderer.CreateHeading(heading);
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
                yield return _paragraphRenderer.CreateParagraph(new ParagraphBlock
                {
                    StyleId = StyleIds.Quote,
                    Inlines = quote.Inlines
                });
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
                yield return new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody }),
                    _noteManager.CreateFootnoteReference(footnote.NoteId, footnote.Inlines));
                break;
            case EndnoteBlock endnote:
                yield return new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody }),
                    _noteManager.CreateEndnoteReference(endnote.NoteId, endnote.Inlines));
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
                paragraph.PrependChild(new W.Run(new W.Text("• ")));
            }

            yield return paragraph;
        }
    }

    private void RenderDefaultCover(W.Body body)
    {
        body.AppendChild(_paragraphRenderer.CreatePlainParagraph(_metadata.Title, StyleIds.TocTitle, TextAlignment.Center));
        body.AppendChild(_paragraphRenderer.CreatePlainParagraph($"作者：{_metadata.Author}", StyleIds.ThesisBody, TextAlignment.Center));
        body.AppendChild(_paragraphRenderer.CreatePlainParagraph($"学号：{_metadata.StudentId}", StyleIds.ThesisBody, TextAlignment.Center));
        body.AppendChild(_paragraphRenderer.CreatePlainParagraph($"学院：{_metadata.College}", StyleIds.ThesisBody, TextAlignment.Center));
        body.AppendChild(_paragraphRenderer.CreatePlainParagraph($"专业：{_metadata.Major}", StyleIds.ThesisBody, TextAlignment.Center));
        body.AppendChild(_paragraphRenderer.CreatePlainParagraph($"导师：{_metadata.Advisor}", StyleIds.ThesisBody, TextAlignment.Center));
        body.AppendChild(_paragraphRenderer.CreatePlainParagraph(_metadata.Date, StyleIds.ThesisBody, TextAlignment.Center));
    }
}
