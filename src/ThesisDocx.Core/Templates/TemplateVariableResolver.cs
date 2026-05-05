using System.Globalization;
using System.Security;
using System.Text.RegularExpressions;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;

namespace ThesisDocx.Core.Templates;

public sealed class TemplateVariableResolver
{
    public IReadOnlyList<TemplateVariableResolution> Resolve(
        TemplatePackage package,
        ThesisDocument? document,
        IReadOnlyDictionary<string, string> cliVariables,
        List<TemplateIssue> errors,
        List<TemplateIssue> warnings)
    {
        var resolved = new List<TemplateVariableResolution>();
        foreach (var variable in package.Variables.OrderBy(v => v.DisplayOrder).ThenBy(v => v.Name, StringComparer.Ordinal))
        {
            var fullVariableKey = $"variables.{variable.Name}";
            string? value = null;
            var source = "missing";

            if (TryGetCliValue(cliVariables, fullVariableKey, out value) || TryGetCliValue(cliVariables, variable.Name, out value))
            {
                source = "cli";
            }
            else if (!string.IsNullOrWhiteSpace(variable.SourcePath) && document is not null && TryResolveDocumentPath(document, variable.SourcePath, out value))
            {
                source = "metadata";
            }
            else if (variable.DefaultValue is not null)
            {
                value = variable.DefaultValue.ToString();
                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                {
                    value = value[1..^1];
                }

                source = "default";
            }

            if (!string.IsNullOrWhiteSpace(value) && variable.Type == TemplateVariableType.Date && !string.IsNullOrWhiteSpace(variable.Format))
            {
                value = FormatDate(value, variable.Format);
            }

            if (string.IsNullOrEmpty(value))
            {
                if (variable.Required)
                {
                    errors.Add(new TemplateIssue
                    {
                        Code = "template.variable.requiredMissing",
                        Path = $"$.variables.{variable.Name}",
                        Message = $"Required variable '{variable.Name}' could not be resolved."
                    });
                }
                else
                {
                    warnings.Add(new TemplateIssue
                    {
                        Code = "template.variable.optionalMissing",
                        Path = $"$.variables.{variable.Name}",
                        Message = $"Optional variable '{variable.Name}' was not resolved."
                    });
                    value = string.Empty;
                }
            }

            resolved.Add(new TemplateVariableResolution
            {
                Name = variable.Name,
                Value = EscapeXmlText(value ?? string.Empty),
                Source = source,
                Resolved = !string.IsNullOrEmpty(value)
            });
        }

        return resolved;
    }

    public string ResolveText(string template, TemplatePackage package, ThesisDocument document, IReadOnlyDictionary<string, string> values)
    {
        return Regex.Replace(template, @"\{\{(?<expr>[^}]+)\}\}", match =>
        {
            var expression = match.Groups["expr"].Value.Trim();
            if (expression.StartsWith("date:", StringComparison.Ordinal))
            {
                return DateTime.UtcNow.ToString(expression[5..], CultureInfo.InvariantCulture);
            }

            if (expression.StartsWith("metadata.", StringComparison.Ordinal) && TryResolveDocumentPath(document, expression, out var metadataValue))
            {
                return EscapeXmlText(metadataValue ?? string.Empty);
            }

            if (expression.StartsWith("variables.", StringComparison.Ordinal) && values.TryGetValue(expression[10..], out var variableValue))
            {
                return EscapeXmlText(variableValue);
            }

            return expression switch
            {
                "template.school" => EscapeXmlText(package.School),
                "template.college" => EscapeXmlText(package.College),
                _ => string.Empty
            };
        }, RegexOptions.CultureInvariant);
    }

    public static string EscapeXmlText(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private static bool TryGetCliValue(IReadOnlyDictionary<string, string> cliVariables, string key, out string? value)
    {
        if (cliVariables.TryGetValue(key, out var direct))
        {
            value = direct;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryResolveDocumentPath(ThesisDocument document, string path, out string? value)
    {
        value = path switch
        {
            "metadata.title" => document.Metadata.Title,
            "metadata.subtitle" => document.Metadata.Subtitle,
            "metadata.author" => document.Metadata.Author,
            "metadata.college" => document.Metadata.College,
            "metadata.major" => document.Metadata.Major,
            "metadata.studentId" => document.Metadata.StudentId,
            "metadata.advisor" => document.Metadata.Advisor,
            "metadata.date" => document.Metadata.Date,
            "metadata.language" => document.Metadata.Language,
            _ => null
        };
        return value is not null;
    }

    private static string FormatDate(string value, string format)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date.ToString(format, CultureInfo.InvariantCulture)
            : value;
    }
}
