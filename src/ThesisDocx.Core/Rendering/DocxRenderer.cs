using ThesisDocx.Core.Models;

namespace ThesisDocx.Core.Rendering;

public sealed class DocxRenderer
{
    public void Render(ThesisDocument document, ThesisFormatSpec format, string outputPath)
    {
        Render(document, format, outputPath, null);
    }

    public void Render(ThesisDocument document, ThesisFormatSpec format, string outputPath, DocxRenderContext? context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(format);

        new DocumentPackageBuilder().Build(document, format, outputPath, context);
    }
}
