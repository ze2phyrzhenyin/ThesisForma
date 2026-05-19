using System.Text.Json;
using System.Text.Json.Nodes;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Templates;

public sealed class TemplateResolver
{
    private readonly TemplateLoader _loader = new();
    private readonly TemplateRegistry _registry = new();
    private readonly FormatSpecMerger _merger = new();

    public TemplateResolutionResult Resolve(string templatePath, ThesisDocument? document = null, IReadOnlyDictionary<string, string>? cliVariables = null)
    {
        var result = new TemplateResolutionResult();
        var stack = new HashSet<string>(StringComparer.Ordinal);
        var template = ResolveTemplate(templatePath, result, stack);
        if (template is null)
        {
            return result;
        }

        result.Template = template;
        result.PageTemplates = template.PageTemplates.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        result.Assets = new TemplateAssetResolver().Resolve(template, result.Errors).ToList();
        result.Variables = new TemplateVariableResolver()
            .Resolve(template, document, cliVariables ?? new Dictionary<string, string>(StringComparer.Ordinal), result.Errors, result.Warnings)
            .ToList();

        result.FormatSpec = LoadFormatSpec(template, result);
        return result;
    }

    private TemplatePackage? ResolveTemplate(string templatePath, TemplateResolutionResult result, HashSet<string> stack)
    {
        var template = _loader.Load(templatePath);
        if (!TemplateSchemaVersions.IsSupported(template.TemplateSchemaVersion))
        {
            result.Errors.Add(new TemplateIssue
            {
                Code = "template.schemaVersion.unsupported",
                Path = "$.templateSchemaVersion",
                Message = $"Unsupported templateSchemaVersion '{template.TemplateSchemaVersion}'."
            });
            return template;
        }

        if (!stack.Add(template.Id))
        {
            result.Errors.Add(new TemplateIssue
            {
                Code = "template.inheritance.circular",
                Path = "$.extends.templateId",
                Message = $"Circular template inheritance detected at '{template.Id}'."
            });
            return template;
        }

        if (template.Extends is null)
        {
            stack.Remove(template.Id);
            return template;
        }

        var parent = _registry.FindById(template.TemplateDirectory ?? Directory.GetCurrentDirectory(), template.Extends.TemplateId);
        if (parent is null)
        {
            result.Errors.Add(new TemplateIssue
            {
                Code = "template.inheritance.parentMissing",
                Path = "$.extends.templateId",
                Message = $"Parent template '{template.Extends.TemplateId}' was not found."
            });
            stack.Remove(template.Id);
            return template;
        }

        var resolvedParent = ResolveTemplate(parent.TemplateDirectory!, result, stack);
        stack.Remove(template.Id);
        return resolvedParent is null ? template : MergeTemplate(resolvedParent, template);
    }

    private TemplatePackage MergeTemplate(TemplatePackage parent, TemplatePackage child)
    {
        var merged = new TemplatePackage
        {
            TemplateSchemaVersion = child.TemplateSchemaVersion,
            Id = child.Id,
            Name = child.Name,
            Version = child.Version,
            Locale = string.IsNullOrWhiteSpace(child.Locale) ? parent.Locale : child.Locale,
            Description = string.IsNullOrWhiteSpace(child.Description) ? parent.Description : child.Description,
            School = string.IsNullOrWhiteSpace(child.School) ? parent.School : child.School,
            College = string.IsNullOrWhiteSpace(child.College) ? parent.College : child.College,
            DegreeType = string.IsNullOrWhiteSpace(child.DegreeType) ? parent.DegreeType : child.DegreeType,
            Tags = parent.Tags.Concat(child.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            TemplateDirectory = child.TemplateDirectory,
            FormatSpec = child.FormatSpec ?? parent.FormatSpec,
            FormatSpecRef = child.FormatSpecRef ?? parent.FormatSpecRef,
            DocumentOverrides = child.DocumentOverrides ?? parent.DocumentOverrides,
            Notes = parent.Notes.Concat(child.Notes).ToList(),
            ComplianceRules = MergeByKey(parent.ComplianceRules, child.ComplianceRules, rule => rule.Id),
            Variables = MergeByKey(parent.Variables, child.Variables, variable => variable.Name),
            Assets = MergeByKey(parent.Assets, child.Assets, asset => asset.Id),
            PageTemplates = MergeByKey(parent.PageTemplates, child.PageTemplates, layout => layout.Id)
        };

        var parentFormatNode = LoadRawFormatSpecNode(parent);
        var childFormatNode = LoadRawFormatSpecNode(child);
        if (parentFormatNode is not null && childFormatNode is not null)
        {
            merged.FormatSpec = null;
            merged.FormatSpecRef = null;
            merged.FormatSpec = _merger.Merge(
                parentFormatNode.Deserialize<ThesisFormatSpec>(ThesisJson.Options) ?? new ThesisFormatSpec(),
                childFormatNode);
        }

        return merged;
    }

    private ThesisFormatSpec LoadFormatSpec(TemplatePackage template, TemplateResolutionResult result)
    {
        if (template.FormatSpec is not null)
        {
            return template.FormatSpec;
        }

        var node = LoadRawFormatSpecNode(template);
        if (node is null)
        {
            result.Errors.Add(new TemplateIssue
            {
                Code = "template.formatSpec.missing",
                Path = "$.formatSpec",
                Message = "Template must provide formatSpec or formatSpecRef."
            });
            return new ThesisFormatSpec();
        }

        return node.Deserialize<ThesisFormatSpec>(ThesisJson.Options) ?? new ThesisFormatSpec();
    }

    private static JsonNode? LoadRawFormatSpecNode(TemplatePackage template)
    {
        if (template.FormatSpec is not null)
        {
            return JsonSerializer.SerializeToNode(template.FormatSpec, ThesisJson.Options);
        }

        if (string.IsNullOrWhiteSpace(template.FormatSpecRef))
        {
            return null;
        }

        if (Path.IsPathRooted(template.FormatSpecRef))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(template.TemplateDirectory ?? Directory.GetCurrentDirectory(), template.FormatSpecRef));
        return JsonNode.Parse(File.ReadAllText(path));
    }

    private static List<T> MergeByKey<T>(IEnumerable<T> parent, IEnumerable<T> child, Func<T, string> keySelector)
    {
        var values = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in parent)
        {
            values[keySelector(item)] = item;
        }

        foreach (var item in child)
        {
            values[keySelector(item)] = item;
        }

        return values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList();
    }
}
