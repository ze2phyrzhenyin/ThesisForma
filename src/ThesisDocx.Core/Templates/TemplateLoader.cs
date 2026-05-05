using System.Text.Json;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Utilities;

namespace ThesisDocx.Core.Templates;

public sealed class TemplateLoader
{
    public TemplatePackage Load(string templatePath)
    {
        var directory = Directory.Exists(templatePath)
            ? templatePath
            : Path.GetDirectoryName(Path.GetFullPath(templatePath)) ?? throw new InvalidOperationException($"Invalid template path '{templatePath}'.");
        var jsonPath = Directory.Exists(templatePath) ? Path.Combine(templatePath, "template.json") : templatePath;
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException($"Template file not found: {jsonPath}", jsonPath);
        }

        var package = JsonSerializer.Deserialize<TemplatePackage>(File.ReadAllText(jsonPath), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize template '{jsonPath}'.");
        package.TemplateDirectory = Path.GetFullPath(directory);
        foreach (var asset in package.Assets)
        {
            asset.BaseDirectory = package.TemplateDirectory;
        }

        return package;
    }
}
