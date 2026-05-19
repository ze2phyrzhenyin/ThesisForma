using DocumentFormat.OpenXml;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Utilities;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class FigureRenderer
{
    private readonly RelationshipManager _relationshipManager;
    private readonly ThesisFormatSpec _format;
    private readonly CaptionRenderer _captionRenderer;

    public FigureRenderer(RelationshipManager relationshipManager, ThesisFormatSpec format, CaptionRenderer captionRenderer)
    {
        _relationshipManager = relationshipManager;
        _format = format;
        _captionRenderer = captionRenderer;
    }

    public IEnumerable<OpenXmlElement> Render(FigureBlock block)
    {
        if (string.Equals(_format.Figures.CaptionPosition, "above", StringComparison.OrdinalIgnoreCase))
        {
            yield return _captionRenderer.CreateFigureCaption(block.Caption);
        }

        yield return CreateFigureParagraph(block);

        if (!string.Equals(_format.Figures.CaptionPosition, "above", StringComparison.OrdinalIgnoreCase))
        {
            yield return _captionRenderer.CreateFigureCaption(block.Caption);
        }
    }

    internal W.Paragraph CreateFigureParagraph(FigureBlock block)
    {
        var imageBytes = LoadImageBytes(block);
        var relationshipId = _relationshipManager.AddImagePart(imageBytes, block.ImageContentType);
        var drawingId = _relationshipManager.AllocateDrawingId();
        var widthCm = block.WidthCm ?? _format.Figures.DefaultWidthCm;
        var heightCm = block.HeightCm ?? _format.Figures.DefaultHeightCm ?? widthCm * 0.56;
        var widthEmu = UnitConverter.CentimetersToEmu(widthCm);
        var heightEmu = UnitConverter.CentimetersToEmu(heightCm);

        return new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = StyleIds.ThesisBody },
                new W.Justification { Val = _format.Figures.Center ? W.JustificationValues.Center : W.JustificationValues.Left }),
            new W.Run(CreateDrawing(relationshipId, drawingId, widthEmu, heightEmu, block.Crop)));
    }

    private static byte[] LoadImageBytes(FigureBlock block)
    {
        if (!string.IsNullOrWhiteSpace(block.ImageDataBase64))
        {
            var raw = block.ImageDataBase64;
            var comma = raw.IndexOf(',', StringComparison.Ordinal);
            if (comma >= 0)
            {
                raw = raw[(comma + 1)..];
            }

            return Convert.FromBase64String(raw);
        }

        if (!string.IsNullOrWhiteSpace(block.ImagePath))
        {
            return File.ReadAllBytes(block.ImagePath);
        }

        throw new InvalidOperationException("Figure block requires imageDataBase64 or imagePath.");
    }

    private static W.Drawing CreateDrawing(string relationshipId, uint drawingId, long widthEmu, long heightEmu, FigureCropSpec? crop)
    {
        var blipFill = new PIC.BlipFill(new A.Blip { Embed = relationshipId });
        var sourceRectangle = CreateSourceRectangle(crop);
        if (sourceRectangle is not null)
        {
            blipFill.AppendChild(sourceRectangle);
        }

        blipFill.AppendChild(new A.Stretch(new A.FillRectangle()));

        return new W.Drawing(
            new WP.Inline(
                new WP.Extent { Cx = widthEmu, Cy = heightEmu },
                new WP.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new WP.DocProperties { Id = drawingId, Name = $"Figure {drawingId}" },
                new WP.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = drawingId, Name = $"Figure {drawingId}" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            blipFill,
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                {
                                    Preset = A.ShapeTypeValues.Rectangle
                                })))
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

    private static A.SourceRectangle? CreateSourceRectangle(FigureCropSpec? crop)
    {
        if (crop is null)
        {
            return null;
        }

        var rectangle = new A.SourceRectangle();
        SetCropValue(crop.LeftPercent, value => rectangle.Left = value);
        SetCropValue(crop.TopPercent, value => rectangle.Top = value);
        SetCropValue(crop.RightPercent, value => rectangle.Right = value);
        SetCropValue(crop.BottomPercent, value => rectangle.Bottom = value);
        return rectangle.HasAttributes ? rectangle : null;
    }

    private static void SetCropValue(double? percent, Action<Int32Value> assign)
    {
        if (!percent.HasValue)
        {
            return;
        }

        assign((Int32Value)(int)Math.Round(percent.Value * 1000, MidpointRounding.AwayFromZero));
    }
}
