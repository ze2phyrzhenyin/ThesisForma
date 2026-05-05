using ThesisDocx.Core.Models.Templates;

namespace ThesisDocx.Core.Templates;

public sealed class TemplateRegistry
{
    private readonly TemplateLoader _loader = new();

    public IReadOnlyList<TemplatePackage> ListTemplates(string templatesDirectory)
    {
        if (!Directory.Exists(templatesDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(templatesDirectory)
            .Select(Path.GetFullPath)
            .Where(directory => File.Exists(Path.Combine(directory, "template.json")))
            .Select(_loader.Load)
            .OrderBy(template => template.Id, StringComparer.Ordinal)
            .ThenBy(template => template.Version, StringComparer.Ordinal)
            .ToList();
    }

    public TemplatePackage? FindById(string startingDirectory, string templateId)
    {
        var root = LocateTemplatesRoot(startingDirectory);
        return ListTemplates(root).FirstOrDefault(template => template.Id == templateId);
    }

    private static string LocateTemplatesRoot(string startingDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startingDirectory));
        if (File.Exists(Path.Combine(current.FullName, "template.json")))
        {
            current = current.Parent ?? current;
        }

        while (current is not null)
        {
            if (current.EnumerateFiles("template.json", SearchOption.AllDirectories).Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(startingDirectory);
    }
}
