using DocumentFormat.OpenXml.Packaging;

namespace ThesisDocx.Core.Rendering;

public sealed class RelationshipManager
{
    private readonly MainDocumentPart _mainPart;
    private int _imageRelationshipCounter;
    private int _hyperlinkRelationshipCounter;

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
}
