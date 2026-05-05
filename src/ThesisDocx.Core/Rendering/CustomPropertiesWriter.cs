using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;

namespace ThesisDocx.Core.Rendering;

public sealed class CustomPropertiesWriter
{
    private const string FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";

    public void Write(WordprocessingDocument package, DocxRenderContext? context, string formatSpecVersion)
    {
        var part = package.CustomFilePropertiesPart ?? package.AddCustomFilePropertiesPart();
        part.Properties = new Properties();

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ThesisDocx.RendererVersion"] = context?.RendererVersion ?? "1.0.0",
            ["ThesisDocx.SchemaVersion"] = formatSpecVersion,
            ["ThesisDocx.TemplateId"] = context?.TemplateId ?? string.Empty,
            ["ThesisDocx.TemplateVersion"] = context?.TemplateVersion ?? string.Empty,
            ["ThesisDocx.ResolvedFormatSpecVersion"] = context?.ResolvedFormatSpecVersion ?? formatSpecVersion,
            ["ThesisDocx.RenderedPageTemplates"] = string.Join(",", context?.RenderedPageTemplates ?? []),
            ["ThesisDocx.RenderedVariables"] = string.Join(",", context?.RenderedVariables ?? []),
            ["ThesisDocx.RenderedAssets"] = string.Join(",", context?.RenderedAssets ?? [])
        };

        var propertyId = 2;
        foreach (var value in values.OrderBy(v => v.Key, StringComparer.Ordinal))
        {
            part.Properties.AppendChild(new CustomDocumentProperty(new VTLPWSTR(value.Value))
            {
                FormatId = FormatId,
                PropertyId = propertyId++,
                Name = value.Key
            });
        }

        part.Properties.Save();
    }
}
