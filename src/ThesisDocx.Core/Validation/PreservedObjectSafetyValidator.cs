using System.Xml.Linq;

namespace ThesisDocx.Core.Validation;

public static class PreservedObjectSafetyValidator
{
    private static readonly HashSet<string> AllowedNamespaces = new(StringComparer.Ordinal)
    {
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
        "http://schemas.openxmlformats.org/drawingml/2006/main",
        "http://schemas.openxmlformats.org/drawingml/2006/picture",
        "http://schemas.openxmlformats.org/drawingml/2006/chart",
        "http://schemas.openxmlformats.org/drawingml/2006/diagram",
        "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing",
        "http://schemas.microsoft.com/office/word/2010/wordprocessingShape",
        "urn:schemas-microsoft-com:vml",
        "urn:schemas-microsoft-com:office:office",
        "urn:schemas-microsoft-com:office:word",
        "urn:schemas-microsoft-com:office:excel",
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
    };

    public static ThesisInputValidationResult Validate(string? rawXml, string path, bool allowRelationshipReferences)
    {
        var result = new ThesisInputValidationResult();
        if (string.IsNullOrWhiteSpace(rawXml))
        {
            result.Add("preservedObject.rawXml.missing", path, "Passthrough preserved objects require rawXml.");
            return result;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(rawXml, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            result.Add("preservedObject.rawXml.invalidXml", path, $"Preserved object rawXml is not well-formed XML: {ex.Message}");
            return result;
        }

        var root = document.Root;
        if (root is null || root.Name.NamespaceName != OmmlSafetyValidator.WordNamespace || root.Name.LocalName is not ("drawing" or "pict"))
        {
            result.Add("preservedObject.rawXml.invalidRoot", path, "Preserved object rawXml root must be w:drawing or w:pict.");
            return result;
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            if (!AllowedNamespaces.Contains(element.Name.NamespaceName))
            {
                result.Add("preservedObject.rawXml.unknownNamespace", path, $"Element '{element.Name}' uses an unsupported namespace.");
            }

            if (element.Name.LocalName is "altChunk" or "object" or "OLEObject" or "script")
            {
                result.Add("preservedObject.rawXml.disallowedElement", path, $"Element '{element.Name.LocalName}' is not allowed in preserved object rawXml.");
            }

            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }

                if (attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    || attribute.Value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                    || attribute.Value.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                    || attribute.Value.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    result.Add("preservedObject.rawXml.disallowedAttribute", path, $"Attribute '{attribute.Name}' is not allowed in preserved object rawXml.");
                }

                if (!allowRelationshipReferences
                    && attribute.Name.NamespaceName == "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                    && attribute.Name.LocalName is "id" or "embed" or "link")
                {
                    result.Add("preservedObject.rawXml.relationshipUnsupported", path, $"Relationship attribute '{attribute.Name}' is not supported by safe passthrough yet.");
                }
            }
        }

        return result;
    }
}
