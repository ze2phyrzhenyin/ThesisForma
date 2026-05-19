using DocumentFormat.OpenXml;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Utilities;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class PageTemplateRenderer
{
    private readonly RelationshipManager _relationshipManager;
    private readonly TemplateVariableResolver _variableResolver = new();

    public PageTemplateRenderer(RelationshipManager relationshipManager)
    {
        _relationshipManager = relationshipManager;
    }

    public IEnumerable<OpenXmlElement> Render(TemplatePageLayout layout, ThesisDocument document, TemplatePackageShim template, DocxRenderContext context)
    {
        context.RenderedPageTemplates.Add(layout.Id);
        foreach (var block in layout.Blocks)
        {
            foreach (var element in RenderBlock(block, layout, document, template, context))
            {
                yield return element;
            }
        }
    }

    private IEnumerable<OpenXmlElement> RenderBlock(PageLayoutBlock block, TemplatePageLayout layout, ThesisDocument document, TemplatePackageShim template, DocxRenderContext context)
    {
        switch (block)
        {
            case SpacerLayoutBlock spacer:
                yield return new W.Paragraph(new W.ParagraphProperties(
                    new W.SpacingBetweenLines { After = UnitConverter.CentimetersToTwips(spacer.HeightCm).ToString() }));
                break;
            case TextLayoutBlock text:
                var resolved = Resolve(text.Value, document, template, context);
                if (!text.SkipWhenEmpty || !string.IsNullOrWhiteSpace(resolved))
                {
                    yield return CreateTextParagraph(resolved, text.Style, text.Alignment, text.SpacingBeforePt, text.SpacingAfterPt, text.FontOverride, text.Paragraph);
                }

                break;
            case MetadataFieldLayoutBlock metadata:
                yield return CreateMetadataParagraph(metadata, document, template, context);
                break;
            case FieldTableLayoutBlock fieldTable:
                yield return CreateFieldTable(fieldTable, document, template, context);
                break;
            case DeclarationTextLayoutBlock declaration:
                foreach (var paragraph in declaration.Paragraphs)
                {
                    yield return CreateTextParagraph(Resolve(paragraph, document, template, context), StyleIds.ThesisBody, TextAlignment.Both, 6, 6, null, null);
                }

                foreach (var signature in declaration.SignatureFields)
                {
                    yield return CreateMetadataParagraph(signature, document, template, context);
                }

                break;
            case ImageLayoutBlock image:
                yield return CreateImageParagraph(image, context);
                break;
            case PageBreakLayoutBlock:
                yield return new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page }));
                break;
            case RuleLayoutBlock rule:
                yield return CreateRuleParagraph(rule);
                break;
            case HandwritingAreaLayoutBlock handwriting:
                yield return CreateHandwritingArea(handwriting);
                break;
        }
    }

    private W.Paragraph CreateMetadataParagraph(MetadataFieldLayoutBlock field, ThesisDocument document, TemplatePackageShim template, DocxRenderContext context)
    {
        var value = ResolveFieldValue(field, document, template, context);
        var paragraph = new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = StyleIds.ThesisBody },
                new W.Justification { Val = StyleBuilder.ToJustification(field.Alignment) }),
            CreateRun(string.IsNullOrWhiteSpace(field.Label) ? value : $"{field.Label}: ", field.LabelFont));
        var valueRun = CreateRun(value, field.ValueFont);
        if (field.Underline)
        {
            EnsureRunProperties(valueRun).AppendChild(new W.Underline { Val = W.UnderlineValues.Single });
        }

        paragraph.AppendChild(valueRun);
        return paragraph;
    }

    private W.Table CreateFieldTable(FieldTableLayoutBlock block, ThesisDocument document, TemplatePackageShim template, DocxRenderContext context)
    {
        var table = new W.Table(new W.TableProperties(
            new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
            CreateTableBorders(block.BorderMode)));
        table.AppendChild(new W.TableGrid(
            new W.GridColumn { Width = UnitConverter.CentimetersToTwips(block.LabelColumnWidthCm).ToString() },
            new W.GridColumn { Width = UnitConverter.CentimetersToTwips(block.ValueColumnWidthCm).ToString() }));
        foreach (var row in block.Rows)
        {
            var tableRow = new W.TableRow();
            if (block.RowHeightPt.HasValue)
            {
                tableRow.AppendChild(new W.TableRowProperties(new W.TableRowHeight
                {
                    Val = (UInt32Value)(uint)UnitConverter.PointsToTwips(block.RowHeightPt.Value),
                    HeightType = W.HeightRuleValues.AtLeast
                }));
            }

            foreach (var field in row)
            {
                var labelFont = field.LabelFont ?? block.LabelFont;
                tableRow.AppendChild(CreateCell(field.Label, block.LabelColumnWidthCm, labelFont, labelFont is null));
                tableRow.AppendChild(CreateCell(ResolveFieldValue(field, document, template, context), block.ValueColumnWidthCm, field.ValueFont ?? block.ValueFont, false));
            }

            table.AppendChild(tableRow);
        }

        return table;
    }

    private W.Paragraph CreateImageParagraph(ImageLayoutBlock image, DocxRenderContext context)
    {
        if (!context.Assets.TryGetValue(image.AssetId, out var asset) || !File.Exists(asset.Path))
        {
            throw new InvalidOperationException($"Template asset '{image.AssetId}' is missing.");
        }

        var relationshipId = _relationshipManager.AddImagePart(File.ReadAllBytes(asset.Path), asset.ContentType);
        var drawingId = _relationshipManager.AllocateDrawingId();
        var widthEmu = UnitConverter.CentimetersToEmu(image.WidthCm);
        var heightEmu = UnitConverter.CentimetersToEmu(image.HeightCm ?? image.WidthCm);
        context.RenderedAssets.Add(asset.Id);
        return new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = StyleIds.ThesisBody },
                new W.Justification { Val = StyleBuilder.ToJustification(image.Alignment) }),
            new W.Run(CreateDrawing(relationshipId, drawingId, widthEmu, heightEmu)));
    }

    private W.Paragraph CreateTextParagraph(string text, string styleId, TextAlignment alignment, double? before, double? after, FontFormatSpec? font, ParagraphFormatSpec? paragraph)
    {
        var properties = new W.ParagraphProperties(new W.ParagraphStyleId { Val = styleId });
        if (paragraph is not null)
        {
            properties.AppendChild(StyleBuilder.CreateSpacing(paragraph));
            var indentation = new W.Indentation();
            if (paragraph.FirstLineIndentChars > 0 && font is not null)
            {
                indentation.FirstLine = UnitConverter.PointsToTwips(font.SizePt * paragraph.FirstLineIndentChars).ToString();
            }

            if (paragraph.HangingIndentCm > 0)
            {
                indentation.Hanging = UnitConverter.CentimetersToTwips(paragraph.HangingIndentCm).ToString();
            }

            if (indentation.HasAttributes)
            {
                properties.AppendChild(indentation);
            }
        }
        else if (before.HasValue || after.HasValue)
        {
            properties.AppendChild(new W.SpacingBetweenLines
            {
                Before = UnitConverter.PointsToTwips(before ?? 0).ToString(),
                After = UnitConverter.PointsToTwips(after ?? 0).ToString()
            });
        }

        properties.AppendChild(new W.Justification { Val = StyleBuilder.ToJustification(alignment) });

        var run = CreateRun(text, font);

        return new W.Paragraph(properties, run);
    }

    private static W.Paragraph CreateRuleParagraph(RuleLayoutBlock rule)
    {
        var properties = new W.ParagraphProperties(
            new W.ParagraphStyleId { Val = StyleIds.ThesisBody },
            new W.ParagraphBorders(new W.BottomBorder
            {
                Val = W.BorderValues.Single,
                Size = (UInt32Value)(uint)Math.Clamp((int)Math.Round(rule.ThicknessPt * 8), 2, 96),
                Color = string.IsNullOrWhiteSpace(rule.Color) ? "000000" : rule.Color
            }));

        if (rule.SpacingBeforePt.HasValue || rule.SpacingAfterPt.HasValue)
        {
            properties.AppendChild(new W.SpacingBetweenLines
            {
                Before = UnitConverter.PointsToTwips(rule.SpacingBeforePt ?? 0).ToString(),
                After = UnitConverter.PointsToTwips(rule.SpacingAfterPt ?? 0).ToString()
            });
        }

        properties.AppendChild(new W.Justification { Val = StyleBuilder.ToJustification(rule.Alignment) });
        return new W.Paragraph(properties, new W.Run(new W.Text(string.Empty)));
    }

    private W.TableCell CreateCell(string text, double widthCm, FontFormatSpec? font, bool bold)
    {
        return new W.TableCell(
            new W.TableCellProperties(new W.TableCellWidth { Type = W.TableWidthUnitValues.Dxa, Width = UnitConverter.CentimetersToTwips(widthCm).ToString() }),
            new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody }), CreateRun(text, font, bold)));
    }

    private static W.Run CreateRun(string text, FontFormatSpec? font, bool forceBold = false)
    {
        var run = new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
        if (font is null && !forceBold)
        {
            return run;
        }

        var properties = new W.RunProperties();
        if (font is not null)
        {
            properties.AppendChild(new W.RunFonts { EastAsia = font.EastAsia, Ascii = font.Latin, HighAnsi = font.Latin, ComplexScript = font.Latin });
        }

        if (forceBold || font?.Bold == true)
        {
            properties.AppendChild(new W.Bold());
            properties.AppendChild(new W.BoldComplexScript());
        }

        if (font?.Italic == true)
        {
            properties.AppendChild(new W.Italic());
            properties.AppendChild(new W.ItalicComplexScript());
        }

        if (font is not null)
        {
            properties.AppendChild(new W.FontSize { Val = UnitConverter.PointsToHalfPoints(font.SizePt).ToString() });
            properties.AppendChild(new W.FontSizeComplexScript { Val = UnitConverter.PointsToHalfPoints(font.SizePt).ToString() });
        }

        run.PrependChild(properties);
        return run;
    }

    private static W.RunProperties EnsureRunProperties(W.Run run)
    {
        if (run.RunProperties is not null)
        {
            return run.RunProperties;
        }

        var properties = new W.RunProperties();
        run.PrependChild(properties);
        return properties;
    }

    private static W.Table CreateHandwritingArea(HandwritingAreaLayoutBlock block)
    {
        var table = new W.Table(new W.TableProperties(
            new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
            CreateHandwritingBorders(block)));
        table.AppendChild(new W.TableGrid(new W.GridColumn { Width = "5000" }));
        var row = new W.TableRow(new W.TableRowProperties(new W.TableRowHeight
        {
            Val = (UInt32Value)(uint)UnitConverter.CentimetersToTwips(block.HeightCm),
            HeightType = W.HeightRuleValues.AtLeast
        }));
        row.AppendChild(new W.TableCell(
            new W.TableCellProperties(new W.TableCellWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" }),
            new W.Paragraph(
                new W.ParagraphProperties(
                    new W.ParagraphStyleId { Val = StyleIds.ThesisBody },
                    new W.Justification { Val = StyleBuilder.ToJustification(block.LabelAlignment) }),
                new W.Run(new W.Text(block.Label) { Space = SpaceProcessingModeValues.Preserve }))));
        table.AppendChild(row);
        return table;
    }

    private static W.TableBorders CreateHandwritingBorders(HandwritingAreaLayoutBlock block)
    {
        var (size, color) = HandwritingBorderValues(block);
        return new W.TableBorders(
            new W.TopBorder { Val = W.BorderValues.Single, Size = size, Color = color },
            new W.LeftBorder { Val = W.BorderValues.Single, Size = size, Color = color },
            new W.BottomBorder { Val = W.BorderValues.Single, Size = size, Color = color },
            new W.RightBorder { Val = W.BorderValues.Single, Size = size, Color = color },
            new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = size, Color = color },
            new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = size, Color = color });
    }

    private static (UInt32Value Size, string Color) HandwritingBorderValues(HandwritingAreaLayoutBlock block)
    {
        return (
            (UInt32Value)(uint)Math.Clamp((int)Math.Round(block.BorderThicknessPt * 8), 2, 96),
            string.IsNullOrWhiteSpace(block.BorderColor) ? "000000" : block.BorderColor);
    }


    private string ResolveFieldValue(MetadataFieldLayoutBlock field, ThesisDocument document, TemplatePackageShim template, DocxRenderContext context)
    {
        if (!string.IsNullOrWhiteSpace(field.ValueTemplate))
        {
            return Resolve(field.ValueTemplate, document, template, context);
        }

        if (!string.IsNullOrWhiteSpace(field.VariableName) && context.Variables.TryGetValue(field.VariableName, out var variable))
        {
            context.RenderedVariables.Add(field.VariableName);
            return variable;
        }

        if (!string.IsNullOrWhiteSpace(field.SourcePath))
        {
            return Resolve($"{{{{{field.SourcePath}}}}}", document, template, context);
        }

        return string.Empty;
    }

    private string Resolve(string value, ThesisDocument document, TemplatePackageShim template, DocxRenderContext context)
    {
        return _variableResolver.ResolveText(value, template.Package, document, context.Variables);
    }

    private static W.TableBorders CreateTableBorders(FieldTableBorderMode borderMode)
    {
        var visible = borderMode == FieldTableBorderMode.Full;
        var bottomLine = borderMode == FieldTableBorderMode.BottomLine;
        return new W.TableBorders(
            new W.TopBorder { Val = visible ? W.BorderValues.Single : W.BorderValues.Nil },
            new W.LeftBorder { Val = visible ? W.BorderValues.Single : W.BorderValues.Nil },
            new W.BottomBorder { Val = visible || bottomLine ? W.BorderValues.Single : W.BorderValues.Nil },
            new W.RightBorder { Val = visible ? W.BorderValues.Single : W.BorderValues.Nil },
            new W.InsideHorizontalBorder { Val = visible || bottomLine ? W.BorderValues.Single : W.BorderValues.Nil },
            new W.InsideVerticalBorder { Val = visible ? W.BorderValues.Single : W.BorderValues.Nil });
    }

    private static W.Drawing CreateDrawing(string relationshipId, uint drawingId, long widthEmu, long heightEmu)
    {
        return new W.Drawing(
            new WP.Inline(
                new WP.Extent { Cx = widthEmu, Cy = heightEmu },
                new WP.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new WP.DocProperties { Id = drawingId, Name = $"Template Asset {drawingId}" },
                new WP.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = drawingId, Name = $"Template Asset {drawingId}" },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });
    }
}

public sealed class TemplatePackageShim
{
    public TemplatePackageShim(Models.Templates.TemplatePackage package)
    {
        Package = package;
    }

    public Models.Templates.TemplatePackage Package { get; }
}
