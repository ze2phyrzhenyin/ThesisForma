using DocumentFormat.OpenXml;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Validation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class PreservedObjectRenderer
{
    private readonly ParagraphRenderer _paragraphRenderer;
    private readonly RelationshipManager _relationshipManager;

    public PreservedObjectRenderer(ParagraphRenderer paragraphRenderer, RelationshipManager relationshipManager)
    {
        _paragraphRenderer = paragraphRenderer;
        _relationshipManager = relationshipManager;
    }

    public IEnumerable<OpenXmlElement> Render(PreservedObjectBlock block)
    {
        if (block.PreservationMode == PreservedObjectMode.Passthrough)
        {
            var allowsRelationships = block.Parts.Count > 0;
            var sourceXml = block.RawXml ?? string.Empty;
            if (!PreservedObjectSafetyValidator.Validate(sourceXml, "$.rawXml", allowRelationshipReferences: allowsRelationships).IsValid)
            {
                yield return ReviewFallback(block);
                yield break;
            }

            var relationshipMap = _relationshipManager.AddPreservedPartGraph(block.Parts);
            var rawXml = RelationshipManager.RewriteRelationshipIds(sourceXml, relationshipMap);
            if (!PreservedObjectSafetyValidator.Validate(rawXml, "$.rawXml", allowRelationshipReferences: allowsRelationships).IsValid)
            {
                yield return ReviewFallback(block);
                yield break;
            }

            var run = new W.Run();
            run.InnerXml = rawXml;
            yield return new W.Paragraph(
                new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody }),
                run);
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(block.ExtractedText))
        {
            yield return _paragraphRenderer.CreatePlainParagraph(block.ExtractedText!);
            yield break;
        }

        yield return ReviewFallback(block);
    }

    private W.Paragraph ReviewFallback(PreservedObjectBlock block)
    {
        return _paragraphRenderer.CreatePlainParagraph($"[preserved object requires review: {block.ObjectType}]");
    }
}
