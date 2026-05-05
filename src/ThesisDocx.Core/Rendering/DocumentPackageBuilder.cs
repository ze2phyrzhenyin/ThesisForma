using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Models.Templates;

namespace ThesisDocx.Core.Rendering;

public sealed class DocumentPackageBuilder
{
    public void Build(ThesisDocument document, ThesisFormatSpec format, string outputPath, DocxRenderContext? context = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var package = WordprocessingDocument.Create(outputPath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        package.PackageProperties.Creator = "ThesisDocx";
        package.PackageProperties.Created = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        package.PackageProperties.Modified = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var mainPart = package.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        new StyleBuilder().Build(mainPart, format);
        new NumberingBuilder().Build(mainPart, format);
        new SettingsBuilder().Build(mainPart, format);

        var relationshipManager = new RelationshipManager(mainPart);
        var headerFooterBuilder = new HeaderFooterBuilder(mainPart, format);
        var sectionBuilder = new SectionBuilder(format, headerFooterBuilder, BuildPageSetupOverrides(context));
        var bodyRenderer = new BodyRenderer(mainPart, relationshipManager, format, document.Metadata, document, context);

        var body = mainPart.Document.Body ?? throw new InvalidOperationException("Main document body was not created.");
        SectionProfile? currentProfile = null;

        var sections = document.Sections.ToList();
        if (context?.PageTemplates.Any(template => template.TargetSectionType == Models.Templates.PageTemplateTargetSectionType.Declaration) == true
            && sections.All(section => section.Kind != ThesisSectionKind.OriginalityStatement))
        {
            var insertIndex = Math.Min(sections.FindIndex(section => section.Kind == ThesisSectionKind.Cover) + 1, sections.Count);
            if (insertIndex < 0)
            {
                insertIndex = 0;
            }

            sections.Insert(insertIndex, new ThesisSection { Kind = ThesisSectionKind.OriginalityStatement, StartOnNewPage = true });
        }

        foreach (var section in sections)
        {
            var nextProfile = SectionProfileExtensions.FromSectionKind(section.Kind);
            if (currentProfile is null)
            {
                currentProfile = nextProfile;
            }
            else if (currentProfile.Value != nextProfile)
            {
                body.AppendChild(sectionBuilder.CreateSectionBreakParagraph(currentProfile.Value));
                currentProfile = nextProfile;
            }
            else if (section.StartOnNewPage)
            {
                body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            }

            bodyRenderer.RenderSection(body, section);
        }

        bodyRenderer.SaveNoteParts();
        body.AppendChild(sectionBuilder.CreateSectionProperties(currentProfile ?? SectionProfile.Body));
        new CustomPropertiesWriter().Write(package, context, format.SchemaVersion);
        mainPart.Document.Save();
    }

    private static IReadOnlyDictionary<SectionProfile, PageSetupSpec> BuildPageSetupOverrides(DocxRenderContext? context)
    {
        if (context is null)
        {
            return new Dictionary<SectionProfile, PageSetupSpec>();
        }

        var overrides = new Dictionary<SectionProfile, PageSetupSpec>();
        foreach (var layout in context.PageTemplates.Where(layout => layout.PageSetupOverride is not null))
        {
            overrides[ToSectionProfile(layout.TargetSectionType)] = layout.PageSetupOverride!;
        }

        return overrides;
    }

    private static SectionProfile ToSectionProfile(PageTemplateTargetSectionType target)
    {
        return target switch
        {
            PageTemplateTargetSectionType.Cover => SectionProfile.Cover,
            PageTemplateTargetSectionType.Declaration or PageTemplateTargetSectionType.Abstract or PageTemplateTargetSectionType.Toc => SectionProfile.FrontMatter,
            _ => SectionProfile.Body
        };
    }
}
