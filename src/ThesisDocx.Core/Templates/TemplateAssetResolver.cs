using ThesisDocx.Core.Models.Templates;

namespace ThesisDocx.Core.Templates;

public sealed class TemplateAssetResolver
{
    public IReadOnlyList<ResolvedTemplateAsset> Resolve(TemplatePackage package, List<TemplateIssue> errors)
    {
        var resolved = new List<ResolvedTemplateAsset>();
        foreach (var asset in package.Assets.OrderBy(asset => asset.Id, StringComparer.Ordinal))
        {
            var directory = asset.BaseDirectory ?? package.TemplateDirectory ?? Directory.GetCurrentDirectory();
            if (Path.IsPathRooted(asset.Path))
            {
                errors.Add(new TemplateIssue
                {
                    Code = "template.asset.absolutePath",
                    Path = $"$.assets[{asset.Id}].path",
                    Message = $"Asset '{asset.Id}' uses an absolute path."
                });
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(directory, asset.Path));
            if (!fullPath.StartsWith(Path.GetFullPath(directory), StringComparison.Ordinal))
            {
                errors.Add(new TemplateIssue
                {
                    Code = "template.asset.pathTraversal",
                    Path = $"$.assets[{asset.Id}].path",
                    Message = $"Asset '{asset.Id}' escapes the template directory."
                });
            }

            if (asset.Required && !File.Exists(fullPath))
            {
                errors.Add(new TemplateIssue
                {
                    Code = "template.asset.missing",
                    Path = $"$.assets[{asset.Id}].path",
                    Message = $"Required asset '{asset.Id}' was not found at '{asset.Path}'."
                });
            }

            resolved.Add(new ResolvedTemplateAsset
            {
                Id = asset.Id,
                Type = asset.Type,
                Path = fullPath,
                ContentType = asset.ContentType,
                Required = asset.Required
            });
        }

        return resolved;
    }
}
