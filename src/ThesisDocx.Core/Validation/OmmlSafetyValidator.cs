using System.Xml.Linq;

namespace ThesisDocx.Core.Validation;

public static class OmmlSafetyValidator
{
    public const string MathNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    public const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static ThesisInputValidationResult Validate(string? omml, string path)
    {
        var result = new ThesisInputValidationResult();
        if (string.IsNullOrWhiteSpace(omml))
        {
            result.Add("equation.omml.missing", path, "OMML source is required.");
            return result;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(omml, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            result.Add("equation.omml.invalidXml", path, $"OMML is not well-formed XML: {ex.Message}");
            return result;
        }

        var root = document.Root;
        if (root is null || root.Name.NamespaceName != MathNamespace || root.Name.LocalName is not ("oMath" or "oMathPara"))
        {
            result.Add("equation.omml.invalidRoot", path, "OMML root must be m:oMath or m:oMathPara.");
            return result;
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.NamespaceName is not MathNamespace and not WordNamespace)
            {
                result.Add("equation.omml.unknownNamespace", path, $"Element '{element.Name}' uses an unsupported namespace.");
            }

            if (element.Name.LocalName is "altChunk")
            {
                result.Add("equation.omml.disallowedElement", path, "altChunk is not allowed in OMML input.");
            }

            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }

                if (attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    || attribute.Value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                    || attribute.Name.LocalName is "embed" or "link" or "id")
                {
                    result.Add("equation.omml.disallowedAttribute", path, $"Attribute '{attribute.Name}' is not allowed in OMML input.");
                }
            }
        }

        return result;
    }
}
