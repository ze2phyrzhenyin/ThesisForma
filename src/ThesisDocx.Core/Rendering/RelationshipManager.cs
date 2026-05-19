using DocumentFormat.OpenXml.Packaging;
using System.Xml.Linq;
using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Rendering;

public sealed class RelationshipManager
{
    private readonly MainDocumentPart _mainPart;
    private int _imageRelationshipCounter;
    private int _hyperlinkRelationshipCounter;
    private int _preservedRelationshipCounter;

    public RelationshipManager(MainDocumentPart mainPart)
    {
        _mainPart = mainPart;
    }

    public uint NextDrawingId { get; private set; } = 1;

    public uint NextBookmarkId { get; private set; } = 1;

    public string AddImagePart(byte[] bytes, string contentType)
    {
        var relationshipId = $"rIdImage{++_imageRelationshipCounter}";
        var imagePart = _mainPart.AddImagePart(ToImagePartType(contentType), relationshipId);
        using var stream = new MemoryStream(bytes);
        imagePart.FeedData(stream);
        return relationshipId;
    }

    public string AddHyperlink(Uri uri)
    {
        var relationshipId = $"rIdHyperlink{++_hyperlinkRelationshipCounter}";
        _mainPart.AddHyperlinkRelationship(uri, true, relationshipId);
        return relationshipId;
    }

    public Dictionary<string, string> AddPreservedPartGraph(IReadOnlyList<PreservedObjectPart> parts)
    {
        var relationshipMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            var newRelationshipId = AddPreservedPart(_mainPart, part);
            relationshipMap[part.RelationshipId] = newRelationshipId;
        }

        return relationshipMap;
    }

    public static string RewriteRelationshipIds(string xml, IReadOnlyDictionary<string, string> relationshipMap)
    {
        if (relationshipMap.Count == 0 || string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        var element = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
        foreach (var candidate in element.DescendantsAndSelf())
        {
            foreach (var attribute in candidate.Attributes().Where(attribute =>
                         attribute.Name.NamespaceName == "http://schemas.openxmlformats.org/officeDocument/2006/relationships").ToList())
            {
                if (relationshipMap.TryGetValue(attribute.Value, out var replacement))
                {
                    attribute.Value = replacement;
                }
            }
        }

        return element.ToString(SaveOptions.DisableFormatting);
    }

    public uint AllocateDrawingId()
    {
        return NextDrawingId++;
    }

    public uint AllocateBookmarkId()
    {
        return NextBookmarkId++;
    }

    private static PartTypeInfo ToImagePartType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ImagePartType.Jpeg,
            "image/gif" => ImagePartType.Gif,
            "image/bmp" => ImagePartType.Bmp,
            "image/tiff" => ImagePartType.Tiff,
            _ => ImagePartType.Png
        };
    }

    private string AddPreservedPart(OpenXmlPartContainer parent, PreservedObjectPart sourcePart)
    {
        var relationshipId = $"rIdPreserved{++_preservedRelationshipCounter}";
        var part = CreatePreservedPart(parent, sourcePart, relationshipId);
        var childRelationshipMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in sourcePart.Children)
        {
            childRelationshipMap[child.RelationshipId] = AddPreservedPart(part, child);
        }

        var bytes = Convert.FromBase64String(sourcePart.DataBase64);
        if (sourcePart.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase) && childRelationshipMap.Count > 0)
        {
            var xml = System.Text.Encoding.UTF8.GetString(bytes);
            bytes = System.Text.Encoding.UTF8.GetBytes(RewriteRelationshipIds(xml, childRelationshipMap));
        }

        using var stream = new MemoryStream(bytes);
        part.FeedData(stream);
        return relationshipId;
    }

    private static OpenXmlPart CreatePreservedPart(OpenXmlPartContainer parent, PreservedObjectPart sourcePart, string relationshipId)
    {
        if (sourcePart.RelationshipType.EndsWith("/image", StringComparison.OrdinalIgnoreCase))
        {
            return AddImagePart(parent, sourcePart.ContentType, relationshipId);
        }

        return sourcePart.RelationshipType switch
        {
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" => parent.AddNewPart<ChartPart>(relationshipId),
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chartUserShapes" => parent.AddNewPart<ChartDrawingPart>(relationshipId),
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/themeOverride" => parent.AddNewPart<ThemeOverridePart>(relationshipId),
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramColors" => parent.AddNewPart<DiagramColorsPart>(relationshipId),
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramData" => parent.AddNewPart<DiagramDataPart>(relationshipId),
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramLayout" => parent.AddNewPart<DiagramLayoutDefinitionPart>(relationshipId),
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramQuickStyle" => parent.AddNewPart<DiagramStylePart>(relationshipId),
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/diagramDrawing" => parent.AddNewPart<DiagramPersistLayoutPart>(relationshipId),
            "http://schemas.microsoft.com/office/2011/relationships/chartStyle" => parent.AddNewPart<ChartStylePart>(sourcePart.ContentType, relationshipId),
            "http://schemas.microsoft.com/office/2011/relationships/chartColorStyle" => parent.AddNewPart<ChartColorStylePart>(sourcePart.ContentType, relationshipId),
            _ => throw new InvalidOperationException($"Unsupported preserved object relationship type '{sourcePart.RelationshipType}'.")
        };
    }

    private static ImagePart AddImagePart(OpenXmlPartContainer parent, string contentType, string relationshipId)
    {
        var partType = ToImagePartType(contentType);
        return parent switch
        {
            MainDocumentPart part => part.AddImagePart(partType, relationshipId),
            ChartPart part => part.AddImagePart(partType, relationshipId),
            ChartDrawingPart part => part.AddImagePart(partType, relationshipId),
            ExtendedChartPart part => part.AddImagePart(partType, relationshipId),
            DiagramDataPart part => part.AddImagePart(partType, relationshipId),
            DiagramLayoutDefinitionPart part => part.AddImagePart(partType, relationshipId),
            DiagramPersistLayoutPart part => part.AddImagePart(partType, relationshipId),
            _ => throw new InvalidOperationException($"Parent part '{parent.GetType().Name}' does not support preserved image relationships.")
        };
    }
}
