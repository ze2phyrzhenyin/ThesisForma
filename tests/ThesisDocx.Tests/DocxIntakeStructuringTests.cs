using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Structuring;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.ContentPreservation;
using ThesisDocx.Tests.Fixtures;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using V = DocumentFormat.OpenXml.Vml;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class DocxIntakeStructuringTests
{
    private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [Fact]
    public void DocxExtraction_ShouldExtractParagraphs()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.Paragraphs, p => p.Text.Contains("中文摘要", StringComparison.Ordinal));
        Assert.True(result.Paragraphs.Count >= 10);
    }

    [Fact]
    public void DocxExtraction_ShouldPreserveParagraphOrder()
    {
        var result = ExtractSynthetic();

        Assert.Equal(Enumerable.Range(0, result.Paragraphs.Count), result.Paragraphs.Select(p => p.Index));
    }

    [Fact]
    public void DocxExtraction_ShouldExtractRuns()
    {
        var result = ExtractSynthetic();
        var run = result.Paragraphs.SelectMany(p => p.Runs).First(r => r.Text.Contains("加粗", StringComparison.Ordinal));

        Assert.True(run.Bold);
    }

    [Fact]
    public void DocxExtraction_ShouldExtractRunItalicUnderlineAndScript()
    {
        var result = ExtractSynthetic();
        var paragraph = result.Paragraphs.First(p => p.Text.Contains("斜体", StringComparison.Ordinal));

        Assert.True(paragraph.RunSummary.HasItalic);
        Assert.True(paragraph.RunSummary.HasUnderline);
        Assert.True(paragraph.RunSummary.HasSuperscript);
        Assert.True(paragraph.RunSummary.HasSubscript);
    }

    [Fact]
    public void DocxExtraction_ShouldExtractTables()
    {
        var result = ExtractSynthetic();

        Assert.Single(result.Tables);
        Assert.Equal("变量 值 A 1", result.Tables[0].Text);
    }

    [Fact]
    public void DocxExtraction_ShouldExtractTableCells()
    {
        var table = ExtractSynthetic().Tables[0];

        Assert.Equal("变量", table.Rows[0].Cells[0].Text);
        Assert.Equal("tables[0].rows[0].cells[0]", table.Rows[0].Cells[0].EvidencePath);
    }

    [Fact]
    public void DocxExtraction_ShouldExtractMergedTableCellMetadata()
    {
        var table = ExtractMergedTable().Tables.Single();

        Assert.Equal(2, table.Rows[0].Cells[0].GridSpan);
        Assert.Equal("restart", table.Rows[0].Cells[0].VerticalMerge);
        Assert.Equal(2, table.Rows[1].Cells[0].GridSpan);
        Assert.Equal("continue", table.Rows[1].Cells[0].VerticalMerge);
    }

    [Fact]
    public void DocxExtraction_ShouldExtractFigureArtifactsDimensionsAndCaptions()
    {
        var directory = NewTempDirectory();
        var docx = CreateMediaTableDocx(directory);

        var result = new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, ArtifactsDirectory = Path.Combine(directory, "artifacts") });
        var figure = result.Figures.Single(f => f.EvidencePath == "paragraphs[1]");

        Assert.Equal("图1 系统架构示意图", figure.SuggestedCaption);
        Assert.Equal("paragraphs[2]", figure.CaptionEvidencePath);
        Assert.Equal("inline", figure.AnchorType);
        Assert.Equal(3.2, figure.WidthCm!.Value, 1);
        Assert.Equal(1.8, figure.HeightCm!.Value, 1);
        Assert.Equal(10, figure.Crop!.LeftPercent);
        Assert.Equal(5, figure.Crop.TopPercent);
        Assert.Equal(20, figure.Crop.RightPercent);
        Assert.False(string.IsNullOrWhiteSpace(figure.ArtifactPath));
        Assert.True(File.Exists(Path.Combine(directory, "artifacts", figure.ArtifactPath!)));
    }

    [Fact]
    public void DocxExtraction_ShouldExtractTableCaptionsGeometryAndCellFigures()
    {
        var directory = NewTempDirectory();
        var docx = CreateMediaTableDocx(directory);

        var result = new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, ArtifactsDirectory = Path.Combine(directory, "artifacts") });
        var table = Assert.Single(result.Tables);
        var imageCell = table.Rows[1].Cells[1];

        Assert.Equal("表1 数据表", table.SuggestedCaption);
        Assert.Equal("paragraphs[3]", table.CaptionEvidencePath);
        Assert.Equal("before", table.CaptionPosition);
        Assert.Equal(100, table.WidthPercent);
        Assert.Contains("insideV", table.Borders);
        Assert.True(table.Rows[0].IsHeader);
        Assert.Equal(2400, table.Rows[0].Cells[0].WidthTwips);
        Assert.Equal("center", table.Rows[0].Cells[0].VerticalAlignment);
        Assert.Equal("D9EAF7", table.Rows[0].Cells[0].Shading);
        Assert.Contains("double", table.Rows[0].Cells[0].Borders);
        var figureId = Assert.Single(imageCell.FigureIds);
        Assert.Contains(result.Figures, figure => figure.Id == figureId && figure.EvidencePath == "tables[0].rows[1].cells[1]");
        var nested = Assert.Single(imageCell.NestedTables);
        Assert.Equal("内层", nested.Text);
    }

    [Fact]
    public void DocxExtraction_ShouldRecordTextBoxDrawingObjectsAsEvidence()
    {
        var directory = NewTempDirectory();
        var docx = CreateTextBoxDocx(directory);

        var extraction = new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx });
        var drawing = Assert.Single(extraction.DrawingObjects);

        Assert.Equal(string.Empty, extraction.Paragraphs[0].Text);
        Assert.Equal("textBox", drawing.ObjectType);
        Assert.Equal("文本框内容", drawing.Text);
        Assert.Contains("<w:pict", drawing.RawXml);
        Assert.Equal("paragraphs[0]", drawing.EvidencePath);

        var structured = new ThesisStructureMapper().Map(extraction, "textbox-extraction.json");
        var preserved = structured.Document.Sections.SelectMany(section => section.Blocks).OfType<PreservedObjectBlock>().Single();
        Assert.Equal(PreservedObjectType.TextBox, preserved.ObjectType);
        Assert.Equal(PreservedObjectMode.Passthrough, preserved.PreservationMode);
        Assert.Equal("文本框内容", preserved.ExtractedText);
        Assert.Contains(structured.UnresolvedItems, item => item.Code == "preservedObject.textBox.reviewRequired");
    }

    [Fact]
    public void DocxExtraction_ShouldExtractChartPartGraphForPreservedObjects()
    {
        var directory = NewTempDirectory();
        var docx = CreateChartDocx(directory);

        var extraction = new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx });
        var drawing = Assert.Single(extraction.DrawingObjects);
        var part = Assert.Single(drawing.Parts);

        Assert.Equal("chart", drawing.ObjectType);
        Assert.Equal("rIdChartSource", drawing.RelationshipId);
        Assert.Equal("rIdChartSource", part.RelationshipId);
        Assert.Equal("http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart", part.RelationshipType);
        Assert.False(string.IsNullOrWhiteSpace(part.DataBase64));

        var structured = new ThesisStructureMapper().Map(extraction, "chart-extraction.json");
        var preserved = structured.Document.Sections.SelectMany(section => section.Blocks).OfType<PreservedObjectBlock>().Single();
        Assert.Equal(PreservedObjectType.Chart, preserved.ObjectType);
        Assert.Equal(PreservedObjectMode.Passthrough, preserved.PreservationMode);
        Assert.Equal("rIdChartSource", Assert.Single(preserved.Parts).RelationshipId);
    }

    [Fact]
    public void DocxExtraction_ShouldExtractFootnotes()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.Footnotes, n => n.NoteId == "2" && n.Text.Contains("脚注内容", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxExtraction_ShouldExtractEndnotes()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.Endnotes, n => n.NoteId == "3" && n.Text.Contains("尾注内容", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxExtraction_ShouldKeepFootnoteReferenceInBody()
    {
        var result = ExtractSynthetic();
        var paragraph = result.Paragraphs.Single(p => p.Text.Contains("正文引用", StringComparison.Ordinal));

        Assert.Contains("[^fn2]", paragraph.Text, StringComparison.Ordinal);
        Assert.Contains("[^en3]", paragraph.Text, StringComparison.Ordinal);
        Assert.Equal(["2"], paragraph.FootnoteReferenceIds);
        Assert.Equal(["3"], paragraph.EndnoteReferenceIds);
    }

    [Fact]
    public void DocxExtraction_ShouldExtractFields()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.Fields, f => f.FieldType == "TOC");
        Assert.Contains(result.Fields, f => f.FieldType == "PAGE");
        Assert.Contains(result.Fields, f => f.FieldType == "REF");
    }

    [Fact]
    public void DocxExtraction_ShouldExtractBookmarks()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.Bookmarks, b => b.Name == "bmIntro");
    }

    [Fact]
    public void DocxExtraction_ShouldExtractHyperlinks()
    {
        var result = ExtractSynthetic();
        var paragraph = result.Paragraphs.Single(p => p.Text == "OpenAI");
        var run = paragraph.Runs.Single();

        Assert.Contains(result.Hyperlinks, h => h.Text == "OpenAI" && h.Uri!.Contains("example.com", StringComparison.Ordinal));
        Assert.Equal("OpenAI", run.Text);
        Assert.False(string.IsNullOrWhiteSpace(run.HyperlinkRelationshipId));
        Assert.Contains("example.com", run.HyperlinkUri!, StringComparison.Ordinal);
    }

    [Fact]
    public void DocxExtraction_ShouldExtractStylesAndNumbering()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.Styles, s => s.StyleId == "Heading1");
        Assert.Contains(result.Numbering, n => n.NumberingId == "1");
    }

    [Fact]
    public void DocxExtraction_ShouldResolveEffectiveFormattingFromStylesAndNumbering()
    {
        var result = ExtractSynthetic();
        var heading = result.Paragraphs.First(p => p.Text == "摘要");

        Assert.Equal("Heading1", heading.EffectiveFormat.StyleId);
        Assert.Equal(["Normal", "Heading1"], heading.EffectiveFormat.StyleChain);
        Assert.Equal("黑体", heading.EffectiveFormat.EastAsiaFont);
        Assert.Equal("Times New Roman", heading.EffectiveFormat.Font);
        Assert.Equal(16, heading.EffectiveFormat.FontSizePt);
        Assert.True(heading.EffectiveFormat.Bold);
        Assert.Equal(0, heading.EffectiveFormat.OutlineLevel);
        Assert.Equal("360", heading.EffectiveFormat.LineSpacing);
        Assert.Contains("style:Heading1.paragraph", heading.EffectiveFormat.Sources);

        var numbered = result.Paragraphs.First(p => p.Text == "1.1 研究背景");
        Assert.Equal("decimal", numbered.EffectiveFormat.NumberingFormat);
        Assert.Equal("%1.", numbered.EffectiveFormat.NumberingText);
        Assert.Equal(720, numbered.EffectiveFormat.LeftIndentTwips);
        Assert.Equal(360, numbered.EffectiveFormat.HangingIndentTwips);
        Assert.Contains("numbering:1/0", numbered.EffectiveFormat.Sources);
    }

    [Fact]
    public void DocxExtraction_ShouldSummarizeEffectiveFormatSignatures()
    {
        var result = ExtractSynthetic();

        Assert.NotEmpty(result.FormatSignatures);
        Assert.Contains(result.FormatSignatures, signature =>
            signature.RepresentativeFormat.StyleId == "Heading1"
            && signature.EvidencePaths.Contains("paragraphs[1]", StringComparer.Ordinal));
    }

    [Fact]
    public void DocxExtraction_ShouldReportFormatChaosAndClusterSimilarBodyFormats()
    {
        var result = ExtractChaotic();

        Assert.Equal("high", result.FormatChaos.ChaosLevel);
        Assert.True(result.FormatChaos.ChaosScore >= 0.6);
        Assert.Contains(result.FormatChaos.Diagnostics, issue => issue.Code == "format.directParagraph.high");
        Assert.Contains(result.FormatChaos.Diagnostics, issue => issue.Code == "format.body.fragmented");
        Assert.Contains(result.ExtractionIssues, issue => issue.Code == "format.signatures.fragmented");

        var bodyCluster = result.FormatClusters.First(cluster => cluster.RoleHint == "body" && cluster.SignatureIds.Count >= 4);
        Assert.Contains("lineSpacing", bodyCluster.Variance);
        Assert.Contains(bodyCluster.Diagnostics, issue => issue.Code == "format.cluster.fragmented");
    }

    [Fact]
    public void FormatCandidates_ShouldGenerateDraftFormatSpecWithEvidenceReport()
    {
        var result = new DocxFormatCandidateGenerator().Generate(ExtractSynthetic(), "synthetic-extraction.json");

        Assert.StartsWith("candidate-", result.CandidateFormatSpec.Name, StringComparison.Ordinal);
        Assert.True(result.Report.GeneratedFieldCount > 0);
        Assert.Contains(result.Report.GeneratedFields, field => field.Path == "$.bodyParagraph.lineSpacingMultiple");
        Assert.Contains(result.Report.ClustersUsed, cluster => cluster.RoleHint == "body");
        Assert.Contains(result.Report.ClustersUsed, cluster => cluster.RoleHint == "heading");
    }

    [Fact]
    public void FormatCandidates_ShouldKeepHighChaosCandidatesNeedsReview()
    {
        var result = new DocxFormatCandidateGenerator().Generate(ExtractChaotic(), "chaotic-extraction.json");

        Assert.Equal("needsReview", result.Report.CandidateStatus);
        Assert.Equal("high", result.Report.ChaosLevel);
        Assert.Contains(result.Report.UnresolvedItems, item => item.Code == "format.chaos.highReviewRequired");
        Assert.Contains(result.Report.GeneratedFields, field => field.Path == "$.bodyParagraph.lineSpacingMultiple");
    }

    [Fact]
    public void FormatCandidates_ShouldValidateSpecAndReportSchemas()
    {
        var directory = NewTempDirectory();
        var result = new DocxFormatCandidateGenerator().Generate(ExtractSynthetic(), "synthetic-extraction.json");
        var specPath = Path.Combine(directory, "candidate-format-spec.json");
        var reportPath = Path.Combine(directory, "format-candidate-report.json");
        File.WriteAllText(specPath, JsonSerializer.Serialize(result.CandidateFormatSpec, ThesisJson.Options));
        File.WriteAllText(reportPath, JsonSerializer.Serialize(result.Report, ThesisJson.Options));

        var validator = new ThesisSchemaValidator();
        var specValidation = validator.ValidateFormatFile(specPath, SchemaPath("thesis-format-spec.schema.json"));
        var reportValidation = validator.ValidateFormatCandidateReportFile(reportPath, SchemaPath("format-candidate-report.schema.json"));

        Assert.True(specValidation.IsValid, string.Join(Environment.NewLine, specValidation.Errors));
        Assert.True(reportValidation.IsValid, string.Join(Environment.NewLine, reportValidation.Errors));
    }

    [Fact]
    public void DocxExtraction_ShouldExtractSectionProperties()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.Sections, s => s.PageSize.Contains("w=", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxExtraction_ShouldIdentifyPossibleHeadings()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.PossibleHeadings, h => h.Text.Contains("引言", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxExtraction_ShouldIdentifyPossibleAbstract()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.PossibleAbstract, a => a.Text == "摘要");
    }

    [Fact]
    public void DocxExtraction_ShouldIdentifyPossibleKeywords()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.PossibleKeywords, k => k.Text.StartsWith("关键词", StringComparison.Ordinal));
    }

    [Fact]
    public void DocxExtraction_ShouldIdentifyPossibleBibliography()
    {
        var result = ExtractSynthetic();

        Assert.Contains(result.PossibleBibliography, b => b.Text == "参考文献");
    }

    [Fact]
    public void DocxExtraction_ShouldWritePlainText()
    {
        var directory = NewTempDirectory();
        var docx = CreateSyntheticDocx(directory);
        var textPath = Path.Combine(directory, "plain.txt");

        new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, PlainTextPath = textPath });

        Assert.Contains("中文摘要正文", File.ReadAllText(textPath));
    }

    [Fact]
    public void DocxExtraction_ShouldWriteMarkdown()
    {
        var directory = NewTempDirectory();
        var docx = CreateSyntheticDocx(directory);
        var markdownPath = Path.Combine(directory, "extracted.md");

        new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, MarkdownPath = markdownPath });

        Assert.Contains("## Paragraphs", File.ReadAllText(markdownPath));
    }

    [Fact]
    public void Schema_ShouldValidateDocxExtraction()
    {
        var directory = NewTempDirectory();
        var docx = CreateSyntheticDocx(directory);
        var extractionPath = Path.Combine(directory, "extraction.json");

        new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, OutputJsonPath = extractionPath });
        var result = new ThesisSchemaValidator().ValidateDocxExtractionFile(extractionPath, SchemaPath("docx-extraction.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void DocxExtraction_ShouldRejectMissingInputWithDiagnostic()
    {
        var ex = AssertExtractionError(new DocxExtractionOptions { InputPath = Path.Combine(NewTempDirectory(), "missing.docx") });

        Assert.Equal("intake.input.notFound", ex.Code);
        Assert.Equal("error", ex.Severity);
        Assert.Equal("$.input", ex.Path);
        Assert.False(string.IsNullOrWhiteSpace(ex.FixHint));
    }

    [Fact]
    public void DocxExtraction_ShouldRejectNonDocxInput()
    {
        var directory = NewTempDirectory();
        var path = Path.Combine(directory, "source.txt");
        File.WriteAllText(path, "not a docx");

        var ex = AssertExtractionError(new DocxExtractionOptions { InputPath = path });

        Assert.Equal("intake.input.notDocx", ex.Code);
    }

    [Fact]
    public void DocxExtraction_ShouldRejectInvalidZip()
    {
        var directory = NewTempDirectory();
        var path = Path.Combine(directory, "invalid.docx");
        File.WriteAllText(path, "not a zip package");

        var ex = AssertExtractionError(new DocxExtractionOptions { InputPath = path });

        Assert.Equal("intake.docx.invalidZip", ex.Code);
    }

    [Fact]
    public void DocxExtraction_ShouldRejectPackageMissingMainDocument()
    {
        var directory = NewTempDirectory();
        var path = Path.Combine(directory, "missing-main.docx");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            archive.CreateEntry("[Content_Types].xml");
        }

        var ex = AssertExtractionError(new DocxExtractionOptions { InputPath = path });

        Assert.Equal("intake.docx.missingDocumentEntry", ex.Code);
    }

    [Fact]
    public void DocxExtraction_ShouldRejectZipEntryPathTraversal()
    {
        var directory = NewTempDirectory();
        var path = Path.Combine(directory, "traversal.docx");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            archive.CreateEntry("../evil.txt");
            archive.CreateEntry("word/document.xml");
        }

        var ex = AssertExtractionError(new DocxExtractionOptions { InputPath = path });

        Assert.Equal("intake.docx.pathTraversal", ex.Code);
    }

    [Fact]
    public void DocxExtraction_ShouldRejectUnsafeExternalRelationshipTarget()
    {
        var directory = NewTempDirectory();
        var docx = CreateSyntheticDocx(directory);
        ReplaceZipEntry(docx, "word/_rels/document.xml.rels", """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rIdUnsafe" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="file:///tmp/private-source.docx" TargetMode="External"/>
        </Relationships>
        """);

        var ex = AssertExtractionError(new DocxExtractionOptions { InputPath = docx });

        Assert.Equal("intake.docx.externalRelationshipUnsafe", ex.Code);
    }

    [Fact]
    public void DocxExtraction_ShouldRejectRelationshipTargetEscapingPackageRoot()
    {
        var directory = NewTempDirectory();
        var docx = CreateSyntheticDocx(directory);
        ReplaceZipEntry(docx, "word/_rels/document.xml.rels", """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rIdUnsafe" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../../private/image.png"/>
        </Relationships>
        """);

        var ex = AssertExtractionError(new DocxExtractionOptions { InputPath = docx });

        Assert.Equal("intake.docx.relationshipTargetInvalid", ex.Code);
    }

    [Fact]
    public void DocxExtraction_ShouldRejectOutputPathOutsideWorkspace()
    {
        var workspace = NewTempDirectory();
        var docx = CreateSyntheticDocx(workspace);
        var outside = Path.Combine(Path.GetDirectoryName(workspace)!, "escape.json");

        var ex = AssertExtractionError(new DocxExtractionOptions
        {
            InputPath = docx,
            OutputJsonPath = outside,
            WorkspaceRoot = workspace
        });

        Assert.Equal("intake.output.pathTraversal", ex.Code);
        Assert.Equal("$.out", ex.Path);
    }

    [Fact]
    public void StructureDraft_ShouldIdentifyChineseAbstract()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.Document.Sections, s => s.Kind == ThesisSectionKind.Abstract && s.Title == "摘要");
    }

    [Fact]
    public void StructureDraft_ShouldIdentifyEnglishAbstract()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.Document.Sections, s => s.Kind == ThesisSectionKind.Abstract && s.Title == "ABSTRACT");
    }

    [Fact]
    public void StructureDraft_ShouldIdentifyToc()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.Document.Sections, s => s.Kind == ThesisSectionKind.Toc);
    }

    [Fact]
    public void StructureDraft_ShouldIdentifyIntroduction()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.Document.Sections.SelectMany(s => s.Blocks).OfType<HeadingBlock>(), h => h.Inlines.OfType<TextInline>().Any(i => i.Text.Contains("引言", StringComparison.Ordinal)));
    }

    [Fact]
    public void StructureDraft_ShouldIdentifyBibliography()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.Document.Sections, s => s.Kind == ThesisSectionKind.Bibliography);
    }

    [Fact]
    public void StructureDraft_ShouldIdentifyAcknowledgments()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.Document.Sections, s => s.Kind == ThesisSectionKind.Acknowledgements);
    }

    [Fact]
    public void StructureDraft_ShouldPreserveOriginalText()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.Document.Sections.SelectMany(s => s.Blocks).OfType<ParagraphBlock>(), p => p.Inlines.OfType<TextInline>().Any(i => i.Text.Contains("中文摘要正文", StringComparison.Ordinal)));
    }

    [Fact]
    public void StructureDraft_ShouldMapFootnoteAndEndnoteReferencesIntoInlineNodes()
    {
        var draft = MapSynthetic();
        var paragraph = draft.Document.Sections
            .SelectMany(s => s.Blocks)
            .OfType<ParagraphBlock>()
            .Single(p => p.Inlines.OfType<TextInline>().Any(i => i.Text.Contains("正文引用", StringComparison.Ordinal)));

        Assert.Contains(paragraph.Inlines, inline => inline is FootnoteInline footnote
            && footnote.NoteId == "fn2"
            && footnote.Inlines.OfType<TextInline>().Any(text => text.Text == "脚注内容"));
        Assert.Contains(paragraph.Inlines, inline => inline is EndnoteInline endnote
            && endnote.NoteId == "en3"
            && endnote.Inlines.OfType<TextInline>().Any(text => text.Text == "尾注内容"));
        Assert.DoesNotContain(paragraph.Inlines.OfType<TextInline>(), inline => inline.Text.Contains("[^fn2]", StringComparison.Ordinal));
        Assert.Equal("match", draft.Report.ContentPreservation.FootnoteComparison.Status);
        Assert.Equal("match", draft.Report.ContentPreservation.EndnoteComparison.Status);
    }

    [Fact]
    public void StructureDraft_ShouldMapHyperlinksIntoInlineNodes()
    {
        var draft = MapSynthetic();
        var paragraph = draft.Document.Sections
            .SelectMany(s => s.Blocks)
            .OfType<ParagraphBlock>()
            .Single(p => p.Inlines.OfType<HyperlinkInline>().Any(i => i.Text == "OpenAI"));

        var hyperlink = paragraph.Inlines.OfType<HyperlinkInline>().Single();
        Assert.Contains("example.com", hyperlink.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(paragraph.Inlines.OfType<TextInline>(), inline => inline.Text == "OpenAI");
        Assert.NotEqual("fail", draft.Report.ContentPreservation.Status);
    }

    [Fact]
    public void StructureDraft_ShouldCreateEvidenceLinks()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.EvidenceLinks, link => link.EvidencePath.StartsWith("paragraphs[", StringComparison.Ordinal));
    }

    [Fact]
    public void StructureDraft_ShouldReportUnresolvedItems()
    {
        var draft = MapSynthetic();

        Assert.Contains(draft.UnresolvedItems, item => item.Code.StartsWith("metadata.", StringComparison.Ordinal));
    }

    [Fact]
    public void StructureDraft_ShouldValidateAgainstThesisDocumentSchema()
    {
        var directory = NewTempDirectory();
        var draft = MapSynthetic();
        var documentPath = Path.Combine(directory, "draft.json");
        File.WriteAllText(documentPath, JsonSerializer.Serialize(draft.Document, ThesisJson.Options));

        var result = new ThesisSchemaValidator().ValidateDocumentFile(documentPath, SchemaPath("thesis-document.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void StructurePrompt_ShouldGeneratePrompt()
    {
        var directory = NewTempDirectory();
        var extractionPath = ExtractSyntheticToFile(directory);

        var prompt = new StructurePromptBuilder().Build(extractionPath);

        Assert.Contains("Do not read or modify `input.docx` directly", prompt);
        Assert.Contains("validate-input", prompt);
    }

    [Fact]
    public void StructurePrompt_ShouldReferenceFormatCandidateReportWhenProvided()
    {
        var directory = NewTempDirectory();
        var extractionPath = ExtractSyntheticToFile(directory);
        var candidateReportPath = Path.Combine(directory, "structured", "format-candidate-report.json");

        var prompt = new StructurePromptBuilder().Build(extractionPath, candidateReportPath);

        Assert.Contains("Candidate format report", prompt);
        Assert.Contains(candidateReportPath, prompt);
        Assert.Contains("human review", prompt);
    }

    [Fact]
    public void StructurePrompt_ShouldGenerateCodexRepairInstructions()
    {
        var workspace = NewTempDirectory();
        var extractionPath = ExtractSyntheticToFile(Path.Combine(workspace, "extraction"));

        var prompt = new StructurePromptBuilder().BuildCodexReview(new CodexStructureReviewOptions
        {
            WorkspacePath = workspace,
            ExtractionPath = extractionPath,
            DocumentPath = Path.Combine(workspace, "structured", "thesis-document.draft.json"),
            MappingReportPath = Path.Combine(workspace, "structured", "structure-mapping-report.json"),
            UnresolvedPath = Path.Combine(workspace, "structured", "unresolved-items.json"),
            EvidencePath = Path.Combine(workspace, "structured", "evidence-links.json"),
            PromptPath = Path.Combine(workspace, "reports", "structure-codex-prompt.md"),
            ReviewReportPath = Path.Combine(workspace, "reports", "structure-codex-review.json"),
            TemplatePath = TemplatePath()
        });

        Assert.Contains("Codex CLI Structure Repair Task", prompt);
        Assert.Contains("第三章", prompt);
        Assert.Contains("Preserve original thesis text exactly", prompt);
    }

    [Fact]
    public void StructureBoundaryAnalyzer_ShouldFlagChapterSequenceRisks()
    {
        var extraction = new DocxExtractionResult
        {
            Paragraphs =
            [
                new ExtractedParagraph { Index = 0, Text = "第一章 绪论", EvidencePath = "paragraphs[0]" },
                new ExtractedParagraph { Index = 1, Text = "第三章 结果", EvidencePath = "paragraphs[1]" }
            ],
            PossibleHeadings =
            [
                new ExtractionEvidence { EvidencePath = "paragraphs[0]", Text = "第一章 绪论", Confidence = 0.9 },
                new ExtractionEvidence { EvidencePath = "paragraphs[1]", Text = "第三章 结果", Confidence = 0.9 }
            ]
        };
        var structured = new ThesisStructuringResult
        {
            EvidenceLinks =
            [
                new ThesisStructureEvidenceLink { EvidencePath = "paragraphs[0]", StructuredPath = "$.sections[0].blocks[0]", Reason = "heading mapped", Confidence = 0.9 },
                new ThesisStructureEvidenceLink { EvidencePath = "paragraphs[1]", StructuredPath = "$.sections[0].blocks[1]", Reason = "heading mapped", Confidence = 0.9 }
            ]
        };

        var report = new StructureBoundaryAnalyzer().Analyze(extraction, structured);

        Assert.Equal("high", report.RiskLevel);
        Assert.True(report.QualityScore < 100);
        Assert.True(report.RecommendCodexReview);
        Assert.Contains(report.Issues, issue => issue.Code == "structure.chapterSequence.gap");
    }

    [Fact]
    public void StructureRepairPlan_ShouldValidateAgainstSchema()
    {
        var directory = NewTempDirectory();
        var planPath = Path.Combine(directory, "structure-repair-plan.json");
        var plan = new StructureRepairPlan
        {
            Summary = "No evidence-backed repair needed for this fixture.",
            Operations = []
        };
        File.WriteAllText(planPath, JsonSerializer.Serialize(plan, ThesisJson.Options));

        var result = new ThesisSchemaValidator().ValidateStructureRepairPlanFile(planPath, SchemaPath("structure-repair-plan.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
    }

    [Fact]
    public void IntakeRegressionManifest_ShouldValidateAgainstSchema()
    {
        var directory = NewTempDirectory();
        var manifestPath = Path.Combine(directory, "intake-regression.json");
        var manifest = new IntakeRegressionManifest
        {
            Name = "private synthetic intake regression",
            WorkspaceRoot = "workspaces",
            DefaultTemplate = TemplatePath(),
            DefaultStructureMode = "auto",
            Cases =
            [
                new IntakeRegressionCase
                {
                    Id = "synthetic",
                    Input = "input/synthetic.docx",
                    StructureMode = "auto",
                    MinimumStructureQualityScore = 1
                }
            ]
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ThesisJson.Options));

        var result = new ThesisSchemaValidator().ValidateIntakeRegressionManifestFile(manifestPath, SchemaPath("intake-regression-manifest.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
    }

    [Fact]
    public void StructureRepairPatchApplier_ShouldMoveBlockByEvidencePathAndRefreshLinks()
    {
        var document = WrongChapterDocument();
        var evidenceLinks = new List<ThesisStructureEvidenceLink>
        {
            new() { EvidencePath = "paragraphs[0]", StructuredPath = "$.sections[0].blocks[0]", Reason = "heading mapped", Confidence = 0.9 },
            new() { EvidencePath = "paragraphs[1]", StructuredPath = "$.sections[0].blocks[1]", Reason = "body paragraph", Confidence = 0.7 },
            new() { EvidencePath = "paragraphs[2]", StructuredPath = "$.sections[0].blocks[2]", Reason = "heading mapped", Confidence = 0.9 }
        };
        var report = new ThesisStructureMappingReport { EvidenceLinks = evidenceLinks };
        var unresolved = new List<ThesisStructureUnresolvedItem>();
        var plan = new StructureRepairPlan
        {
            Summary = "Move third-chapter body under the third-chapter heading.",
            Operations =
            [
                new StructureRepairOperation
                {
                    Id = "move-third-chapter-body",
                    Type = StructureRepairOperationType.MoveBlock,
                    SourceEvidencePath = "paragraphs[1]",
                    AfterEvidencePath = "paragraphs[2]",
                    Reason = "The paragraph belongs after the 第三章 heading.",
                    Confidence = 0.93
                }
            ]
        };

        var apply = new StructureRepairPatchApplier().Apply(document, report, unresolved, evidenceLinks, plan);
        var texts = document.Sections[0].Blocks.Select(BlockText).ToList();

        Assert.Equal("pass", apply.Status);
        Assert.Equal(["第二章 分析", "第三章 结果", "第三章的正文被错误放在第二章末尾。"], texts);
        Assert.Contains(evidenceLinks, link => link.EvidencePath == "paragraphs[1]" && link.StructuredPath == "$.sections[0].blocks[2]");
        Assert.Equal(1, apply.MovedBlockCount);
    }

    [Fact]
    public void StructureRepairPatchApplier_ShouldRejectLowConfidenceMove()
    {
        var document = WrongChapterDocument();
        var evidenceLinks = new List<ThesisStructureEvidenceLink>
        {
            new() { EvidencePath = "paragraphs[0]", StructuredPath = "$.sections[0].blocks[0]", Reason = "heading mapped", Confidence = 0.9 },
            new() { EvidencePath = "paragraphs[1]", StructuredPath = "$.sections[0].blocks[1]", Reason = "body paragraph", Confidence = 0.7 },
            new() { EvidencePath = "paragraphs[2]", StructuredPath = "$.sections[0].blocks[2]", Reason = "heading mapped", Confidence = 0.9 }
        };
        var plan = new StructureRepairPlan
        {
            Summary = "Low confidence move should not be trusted.",
            Operations =
            [
                new StructureRepairOperation
                {
                    Id = "low-confidence-move",
                    Type = StructureRepairOperationType.MoveBlock,
                    SourceEvidencePath = "paragraphs[1]",
                    AfterEvidencePath = "paragraphs[2]",
                    Reason = "Insufficiently certain move.",
                    Confidence = 0.4
                }
            ]
        };

        var apply = new StructureRepairPatchApplier().Apply(document, new ThesisStructureMappingReport(), [], evidenceLinks, plan);

        Assert.Equal("fail", apply.Status);
        Assert.Equal(1, apply.RejectedByTrustCount);
        Assert.Contains(apply.Diagnostics, diagnostic => diagnostic.Code == "structure.repair.lowConfidence");
    }

    [Fact]
    public void Cli_ExtractDocx_ShouldWriteOutputs()
    {
        var directory = NewTempDirectory();
        var docx = CreateSyntheticDocx(directory);
        var extractionPath = Path.Combine(directory, "extraction.json");
        var textPath = Path.Combine(directory, "plain.txt");
        var markdownPath = Path.Combine(directory, "extracted.md");

        var result = CliRunner.Run(RepoRoot(), "extract", "docx", "--input", docx, "--out", extractionPath, "--text", textPath, "--markdown", markdownPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("paragraphs", File.ReadAllText(extractionPath));
        Assert.Contains("中文摘要正文", File.ReadAllText(textPath));
        Assert.Contains("DOCX Extraction", File.ReadAllText(markdownPath));
    }

    [Fact]
    public void Cli_StructureDraft_ShouldWriteOutputs()
    {
        var directory = NewTempDirectory();
        var extractionPath = ExtractSyntheticToFile(directory);
        var documentPath = Path.Combine(directory, "thesis-document.draft.json");
        var reportPath = Path.Combine(directory, "mapping.json");
        var unresolvedPath = Path.Combine(directory, "unresolved.json");

        var result = CliRunner.Run(RepoRoot(), "structure", "draft", "--extraction", extractionPath, "--out", documentPath, "--report", reportPath, "--unresolved", unresolvedPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("sections", File.ReadAllText(documentPath));
        var report = JsonNode.Parse(File.ReadAllText(reportPath))!;
        Assert.True(report["ruleBasedMappedCount"]!.GetValue<int>() > 0);
        Assert.NotEqual("fail", report["contentPreservation"]!["status"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(report["contentPreservation"]!["sourceContentHash"]!.GetValue<string>()));
        Assert.True(JsonNode.Parse(File.ReadAllText(unresolvedPath))!.AsArray().Count > 0);
    }

    [Fact]
    public void StructureMapper_ShouldMapExtractedFiguresIntoFigureBlocks()
    {
        var extraction = FigureExtraction();

        var result = new ThesisStructureMapper().Map(extraction, "extraction/extraction.json");
        var figure = result.Document.Sections.SelectMany(section => section.Blocks).OfType<FigureBlock>().Single();

        Assert.Equal("图1 系统架构示意图展示模块关系。", figure.Caption);
        Assert.Equal("images/image-0.png", figure.ImagePath);
        Assert.Equal("image/png", figure.ImageContentType);
        Assert.Contains(result.EvidenceLinks, link => link.EvidencePath == "paragraphs[0]" && link.Reason.Contains("figure", StringComparison.Ordinal));
        Assert.NotEqual("fail", result.Report.ContentPreservation.Status);
    }

    [Fact]
    public void StructureMapper_ShouldPreserveTableMergeMetadata()
    {
        var result = new ThesisStructureMapper().Map(ExtractMergedTable(), "merged-table-extraction.json");
        var table = result.Document.Sections.SelectMany(section => section.Blocks).OfType<TableBlock>().Single();

        Assert.Equal(2, table.Rows[0].Cells[0].GridSpan);
        Assert.Equal(VerticalMergeKind.Restart, table.Rows[0].Cells[0].VerticalMerge);
        Assert.Equal(2, table.Rows[1].Cells[0].GridSpan);
        Assert.Equal(VerticalMergeKind.Continue, table.Rows[1].Cells[0].VerticalMerge);
        Assert.NotEqual("fail", result.Report.ContentPreservation.Status);
    }

    [Fact]
    public void StructureMapper_ShouldKeepTableOrderCaptionAndCellFigures()
    {
        var directory = NewTempDirectory();
        var docx = CreateMediaTableDocx(directory);
        var extraction = new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, ArtifactsDirectory = Path.Combine(directory, "artifacts") });

        var result = new ThesisStructureMapper().Map(extraction, "media-table-extraction.json");
        var body = result.Document.Sections.First(section => section.Kind == ThesisSectionKind.Body);
        var beforeIndex = body.Blocks.FindIndex(block => block is ParagraphBlock paragraph && paragraph.Inlines.OfType<TextInline>().Any(text => text.Text == "前置正文"));
        var tableIndex = body.Blocks.FindIndex(block => block is TableBlock);
        var afterIndex = body.Blocks.FindIndex(block => block is ParagraphBlock paragraph && paragraph.Inlines.OfType<TextInline>().Any(text => text.Text == "后续正文"));
        var table = (TableBlock)body.Blocks[tableIndex];

        Assert.True(beforeIndex >= 0);
        Assert.True(tableIndex > beforeIndex);
        Assert.True(afterIndex > tableIndex);
        Assert.Equal("表1 数据表", table.Caption);
        Assert.Equal(CaptionPosition.Before, table.CaptionPosition);
        Assert.Equal(TableWidthKind.Percent, table.Width!.Type);
        Assert.Equal(BorderStyleKind.Single, table.Borders!.InsideV!.Style);
        Assert.Equal(BorderStyleKind.Double, table.Rows[0].Cells[0].Borders!.Bottom!.Style);
        Assert.Contains(table.Rows[1].Cells[1].Blocks, block => block is FigureBlock figure
            && figure.ImagePath!.Contains("images/image-", StringComparison.Ordinal)
            && figure.WidthCm.HasValue
            && figure.HeightCm.HasValue);
        Assert.Contains(table.Rows[1].Cells[1].Blocks, block => block is TableBlock nested
            && nested.Rows[0].Cells[0].Text == "内层");
        var figure = table.Rows[1].Cells[1].Blocks.OfType<FigureBlock>().Single();
        Assert.Null(figure.Crop);
        var topLevelFigure = body.Blocks.OfType<FigureBlock>().Single();
        Assert.Equal(10, topLevelFigure.Crop!.LeftPercent);
        Assert.DoesNotContain(body.Blocks.OfType<ParagraphBlock>(), paragraph => paragraph.Inlines.OfType<TextInline>().Any(text => text.Text == "表1 数据表"));
        Assert.NotEqual("fail", result.Report.ContentPreservation.Status);
    }

    [Fact]
    public void Cli_StructureDraft_ShouldRewriteFigureArtifactPathRelativeToDraft()
    {
        var workspace = NewTempDirectory();
        var extractionDirectory = Path.Combine(workspace, "extraction");
        var artifactsDirectory = Path.Combine(workspace, "artifacts", "images");
        var structuredDirectory = Path.Combine(workspace, "structured");
        Directory.CreateDirectory(extractionDirectory);
        Directory.CreateDirectory(artifactsDirectory);
        Directory.CreateDirectory(structuredDirectory);
        File.WriteAllBytes(Path.Combine(artifactsDirectory, "image-0.png"), [137, 80, 78, 71]);
        var extractionPath = Path.Combine(extractionDirectory, "extraction.json");
        File.WriteAllText(extractionPath, JsonSerializer.Serialize(FigureExtraction(), ThesisJson.Options));
        var documentPath = Path.Combine(structuredDirectory, "thesis-document.draft.json");
        var reportPath = Path.Combine(structuredDirectory, "mapping.json");
        var unresolvedPath = Path.Combine(structuredDirectory, "unresolved.json");

        var result = CliRunner.Run(RepoRoot(), "structure", "draft", "--extraction", extractionPath, "--out", documentPath, "--report", reportPath, "--unresolved", unresolvedPath);
        var document = JsonSerializer.Deserialize<ThesisDocument>(File.ReadAllText(documentPath), ThesisJson.Options)!;
        var figure = document.Sections.SelectMany(section => section.Blocks).OfType<FigureBlock>().Single();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("../artifacts/images/image-0.png", figure.ImagePath);
    }

    [Fact]
    public void Cli_StructureDraft_ShouldWriteFailureReportVersion()
    {
        var directory = NewTempDirectory();
        var extractionPath = Path.Combine(directory, "bad-extraction.json");
        var documentPath = Path.Combine(directory, "thesis-document.draft.json");
        var reportPath = Path.Combine(directory, "mapping.json");
        var unresolvedPath = Path.Combine(directory, "unresolved.json");
        File.WriteAllText(extractionPath, "{ not-json");

        var result = CliRunner.Run(RepoRoot(), "structure", "draft", "--extraction", extractionPath, "--out", documentPath, "--report", reportPath, "--unresolved", unresolvedPath);

        Assert.Equal(2, result.ExitCode);
        var json = JsonNode.Parse(File.ReadAllText(reportPath))!;
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        Assert.False(json["success"]!.GetValue<bool>());
        var diagnostic = json["diagnostics"]!.AsArray()[0]!;
        Assert.Equal("intake.structure.failed", diagnostic["code"]!.GetValue<string>());
        Assert.Equal("error", diagnostic["severity"]!.GetValue<string>());
        Assert.Equal("intake", diagnostic["category"]!.GetValue<string>());
    }

    [Fact]
    public void Cli_StructurePrompt_ShouldWritePrompt()
    {
        var directory = NewTempDirectory();
        var extractionPath = ExtractSyntheticToFile(directory);
        var promptPath = Path.Combine(directory, "prompt.md");

        var result = CliRunner.Run(RepoRoot(), "structure", "prompt", "--extraction", extractionPath, "--out", promptPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Codex Structure Review Prompt", File.ReadAllText(promptPath));
    }

    [Fact]
    public void Cli_StructureCodexReview_ShouldInvokeCodexCliAndWriteReport()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var workspace = NewTempDirectory();
        var extractionPath = ExtractSyntheticToFile(Path.Combine(workspace, "extraction"));
        var fakeCodex = CreateFakeCodexCommand(workspace);

        var result = CliRunner.Run(
            RepoRoot(),
            "structure",
            "codex-review",
            "--workspace",
            workspace,
            "--extraction",
            extractionPath,
            "--template",
            TemplatePath(),
            "--codex-command",
            fakeCodex,
            "--timeout-seconds",
            "10");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace, "reports", "fake-codex-marker.txt")));
        var review = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "structure-codex-review.json")))!;
        Assert.Equal("pass", review["status"]!.GetValue<string>());
        Assert.True(review["codexInvoked"]!.GetValue<bool>());
        Assert.Equal(0, review["codexExitCode"]!.GetValue<int>());
        Assert.Equal("warning", review["diagnostics"]!.AsArray().Single(diagnostic => diagnostic!["code"]!.GetValue<string>() == "structure.codex.noDraftChange")!["severity"]!.GetValue<string>());
        Assert.Contains("Codex CLI Structure Repair Task", File.ReadAllText(Path.Combine(workspace, "reports", "structure-codex-prompt.md")));
    }

    [Fact]
    public void ContentPreservationAuditor_ShouldPassForSameExtraction()
    {
        var extraction = ExtractSynthetic();

        var result = new ContentPreservationAuditor().Audit(extraction, extraction);

        Assert.Equal("pass", result.Status);
        Assert.Empty(result.MissingSegments);
        Assert.Equal("match", result.FootnoteComparison.Status);
    }

    [Fact]
    public void ContentPreservationAuditor_ShouldDetectMissingLongSegment()
    {
        var source = ExtractSynthetic();
        var rendered = ExtractSynthetic();
        source.Paragraphs.Add(new ExtractedParagraph
        {
            Id = "p-long",
            Index = 999,
            Text = "这是一段用于内容保真审计测试的长文本片段，长度足以被识别为阻塞级缺失，渲染稿中故意不包含这一段。为了避免短句只产生 warning，这里继续补充原始论文正文样式的连续内容，确保审计器按照长文本丢失处理。",
            EvidencePath = "paragraphs[999]"
        });
        source.PlainText += Environment.NewLine + source.Paragraphs.Last().Text;

        var result = new ContentPreservationAuditor().Audit(source, rendered);

        Assert.Equal("fail", result.Status);
        Assert.Contains(result.MissingSegments, issue => issue.Code == "content.segment.longMissing");
    }

    [Fact]
    public void ContentPreservationAuditor_ShouldAuditStructuredDraftText()
    {
        var source = ExtractSynthetic();
        var structured = new ThesisStructureMapper().Map(source, "synthetic-extraction.json");

        var result = new ContentPreservationAuditor().AuditDraft(source, structured.Document);

        Assert.NotEqual("fail", result.Status);
        Assert.True(result.MatchedSegments > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.SourceContentHash));
        Assert.False(string.IsNullOrWhiteSpace(result.RenderedContentHash));
    }

    [Fact]
    public void ContentPreservationAuditor_ShouldFailDraftAuditWhenLongSourceTextIsDropped()
    {
        var source = ExtractSynthetic();
        source.Paragraphs.Add(new ExtractedParagraph
        {
            Id = "p-long",
            Index = 999,
            Text = "这是一段用于结构化草稿保真审计的超长正文内容，结构映射如果没有把它写进 ThesisDocument 草稿，就必须作为阻塞问题报告出来，防止 intake 阶段静默丢失用户原文。",
            EvidencePath = "paragraphs[999]"
        });
        source.PlainText += Environment.NewLine + source.Paragraphs.Last().Text;
        var draft = new ThesisDocument
        {
            Metadata = new ThesisMetadata { Title = "草稿", Author = "作者", College = "学院", Major = "专业", StudentId = "1", Advisor = "导师", Date = "2026-05-13" },
            Sections =
            [
                new ThesisSection
                {
                    Id = "body",
                    Kind = ThesisSectionKind.Body,
                    Blocks = [new ParagraphBlock { Inlines = [new TextInline { Text = "草稿只保留了一小段内容" }] }]
                }
            ]
        };

        var result = new ContentPreservationAuditor().AuditDraft(source, draft);

        Assert.Equal("fail", result.Status);
        Assert.Contains(result.BlockingIssues, issue => issue.Code == "content.segment.longMissing");
    }

    [Fact]
    public void ContentPreservationAuditor_ShouldIgnoreTemplateAddedCoverText()
    {
        var source = ExtractSynthetic();
        var rendered = ExtractSynthetic();
        rendered.Paragraphs.Insert(0, new ExtractedParagraph
        {
            Id = "p-template",
            Index = -1,
            Text = "北京电影学院毕业论文封面模板新增内容",
            EvidencePath = "paragraphs[-1]"
        });
        rendered.PlainText = "北京电影学院毕业论文封面模板新增内容" + Environment.NewLine + rendered.PlainText;

        var result = new ContentPreservationAuditor().Audit(source, rendered);

        Assert.Equal("pass", result.Status);
        Assert.Contains(result.AddedSegments, issue => issue.Code == "content.segment.templateAdded");
    }

    [Fact]
    public void ContentPreservationAuditor_ShouldCompareFootnoteCounts()
    {
        var source = ExtractSynthetic();
        var rendered = ExtractSynthetic();
        rendered.Footnotes.Clear();

        var result = new ContentPreservationAuditor().Audit(source, rendered);

        Assert.Equal("mismatch", result.FootnoteComparison.Status);
        Assert.Contains(result.Warnings, issue => issue.Code.Contains("footnotes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContentPreservationAuditor_ShouldProduceMarkdownSummary()
    {
        var result = new ContentPreservationAuditor().Audit(ExtractSynthetic(), ExtractSynthetic());
        var markdown = ContentPreservationAuditor.ToMarkdown(result);

        Assert.Contains("Content Preservation Audit", markdown);
        Assert.Contains("Matched segments", markdown);
        Assert.Contains("Source content hash", markdown);
    }

    [Fact]
    public void Cli_ContentAudit_ShouldWriteJsonAndMarkdown()
    {
        var directory = NewTempDirectory();
        var sourcePath = ExtractSyntheticToFile(directory);
        var renderedPath = ExtractSyntheticToFile(directory);
        var outputPath = Path.Combine(directory, "audit.json");
        var markdownPath = Path.Combine(directory, "audit.md");

        var result = CliRunner.Run(RepoRoot(), "content", "audit", "--source-extraction", sourcePath, "--rendered-extraction", renderedPath, "--out", outputPath, "--markdown", markdownPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("pass", JsonNode.Parse(File.ReadAllText(outputPath))!["status"]!.GetValue<string>());
        Assert.Contains("Content Preservation Audit", File.ReadAllText(markdownPath));
    }

    [Fact]
    public void Cli_ExtractDocx_ShouldAcceptDocxAlias()
    {
        var directory = NewTempDirectory();
        var docx = CreateSyntheticDocx(directory);
        var extractionPath = Path.Combine(directory, "extraction-alias.json");

        var result = CliRunner.Run(RepoRoot(), "extract", "docx", "--docx", docx, "--out", extractionPath);

        Assert.Equal(0, result.ExitCode);
        Assert.True(JsonNode.Parse(File.ReadAllText(extractionPath))!["paragraphs"]!.AsArray().Count > 0);
    }

    [Fact]
    public void Cli_ExtractFormatCandidates_ShouldWriteSpecReportAndMarkdown()
    {
        var directory = NewTempDirectory();
        var extractionPath = ExtractSyntheticToFile(directory);
        var specPath = Path.Combine(directory, "candidate-format-spec.json");
        var reportPath = Path.Combine(directory, "format-candidate-report.json");
        var markdownPath = Path.Combine(directory, "format-candidate-report.md");

        var result = CliRunner.Run(RepoRoot(), "extract", "format-candidates", "--extraction", extractionPath, "--out", specPath, "--report", reportPath, "--markdown", markdownPath);

        Assert.Equal(0, result.ExitCode);
        Assert.True(new ThesisSchemaValidator().ValidateFormatFile(specPath, SchemaPath("thesis-format-spec.schema.json")).IsValid);
        Assert.True(new ThesisSchemaValidator().ValidateFormatCandidateReportFile(reportPath, SchemaPath("format-candidate-report.schema.json")).IsValid);
        Assert.Contains("DOCX Format Candidate Report", File.ReadAllText(markdownPath));
    }

    [Fact]
    public void Cli_ExtractDocx_ShouldWriteStructuredDiagnosticForBadInput()
    {
        var directory = NewTempDirectory();
        var output = Path.Combine(directory, "extract-error.json");

        var result = CliRunner.Run(RepoRoot(), "extract", "docx", "--input", Path.Combine(directory, "missing.docx"), "--out", output);

        Assert.Equal(2, result.ExitCode);
        Assert.True(File.Exists(output));
        var json = JsonNode.Parse(File.ReadAllText(output))!;
        Assert.Equal("1.0.0", json["reportVersion"]!.GetValue<string>());
        var diagnostic = json["diagnostics"]!.AsArray()[0]!;
        Assert.Equal("intake.input.notFound", diagnostic["code"]!.GetValue<string>());
        Assert.Equal("error", diagnostic["severity"]!.GetValue<string>());
        Assert.Equal("intake", diagnostic["category"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(diagnostic["fixHint"]!.GetValue<string>()));
    }

    [Fact]
    public void IntakeDocx_ShouldRunExtractionAndDraft()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);

        var result = RunIntake(workspace, input);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace, "extraction", "extraction.json")));
        Assert.True(File.Exists(Path.Combine(workspace, "structured", "thesis-document.draft.json")));
    }

    [Fact]
    public void IntakeDocx_ShouldGenerateStructurePrompt()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);

        RunIntake(workspace, input);

        var prompt = File.ReadAllText(Path.Combine(workspace, "reports", "structure-codex-prompt.md"));
        Assert.Contains("Codex Structure Review Prompt", prompt);
        Assert.Contains("format-candidate-report.json", prompt);
    }

    [Fact]
    public void IntakeDocx_ShouldRunCodexReviewWhenRequested()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);
        var fakeCodex = CreateFakeCodexCommand(workspace);

        var result = CliRunner.Run(
            RepoRoot(),
            "intake",
            "docx",
            "--input",
            input,
            "--workspace",
            workspace,
            "--template",
            TemplatePath(),
            "--codex-review",
            "--codex-command",
            fakeCodex,
            "--timeout-seconds",
            "10");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace, "reports", "fake-codex-marker.txt")));
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;
        Assert.Equal("pass", report["codexReviewStatus"]!.GetValue<string>());
        Assert.Equal(0, report["codexReviewExitCode"]!.GetValue<int>());
        Assert.Contains("structure-codex-review.json", report["codexReviewReportPath"]!.GetValue<string>());
        Assert.True(report["structureQualityScoreAfterCodex"]!.GetValue<int>() > 0);
        Assert.True(File.Exists(Path.Combine(workspace, "reports", "structure-review.md")));
        Assert.True(report["renderAttempted"]!.GetValue<bool>());
    }

    [Fact]
    public void IntakeDocx_ShouldSupportAutoStructureMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);
        var fakeCodex = CreateFakeCodexCommand(workspace);

        var result = CliRunner.Run(
            RepoRoot(),
            "intake",
            "docx",
            "--input",
            input,
            "--workspace",
            workspace,
            "--template",
            TemplatePath(),
            "--structure-mode",
            "auto",
            "--codex-command",
            fakeCodex,
            "--timeout-seconds",
            "10");

        Assert.Equal(0, result.ExitCode);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;
        Assert.Equal("auto", report["structureMode"]!.GetValue<string>());
        Assert.NotEqual("notRun", report["structureAnalysisStatus"]!.GetValue<string>());
        Assert.True(report["structureQualityScore"]!.GetValue<int>() > 0);
        Assert.Contains(report["codexReviewStatus"]!.GetValue<string>(), new[] { "skipped", "pass" });
    }

    [Fact]
    public void IntakeGate_ShouldDefaultToAutoStructureMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);
        var fakeCodex = CreateFakeCodexCommand(workspace);

        var result = CliRunner.Run(
            RepoRoot(),
            "intake",
            "gate",
            "--input",
            input,
            "--workspace",
            workspace,
            "--template",
            TemplatePath(),
            "--codex-command",
            fakeCodex,
            "--timeout-seconds",
            "10");

        Assert.Equal(0, result.ExitCode);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;
        Assert.Equal("auto", report["structureMode"]!.GetValue<string>());
        Assert.NotEqual("notRun", report["structureAnalysisStatus"]!.GetValue<string>());
        Assert.True(report["structureQualityScore"]!.GetValue<int>() > 0);
    }

    [Fact]
    public void IntakeRegression_ShouldRunManifestCases()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = NewTempDirectory();
        var inputDirectory = Path.Combine(directory, "input");
        var input = CreateSyntheticDocx(inputDirectory);
        var fakeCodex = CreateFakeCodexCommand(directory);
        var manifestPath = Path.Combine(directory, "intake-regression.json");
        var reportPath = Path.Combine(directory, "report.json");
        var markdownPath = Path.Combine(directory, "report.md");
        var manifest = new IntakeRegressionManifest
        {
            Name = "private synthetic intake regression",
            WorkspaceRoot = "workspaces",
            DefaultTemplate = Path.GetRelativePath(directory, TemplatePath()),
            DefaultStructureMode = "auto",
            MinimumStructureQualityScore = 1,
            Cases =
            [
                new IntakeRegressionCase
                {
                    Id = "synthetic",
                    Input = Path.GetRelativePath(directory, input),
                    StructureMode = "auto",
                    MaximumUnresolvedCount = 20,
                    ExpectedTextSnippets = ["中文摘要正文"]
                }
            ]
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ThesisJson.Options));

        var result = CliRunner.Run(
            RepoRoot(),
            "intake",
            "regression",
            "--manifest",
            manifestPath,
            "--out",
            reportPath,
            "--markdown",
            markdownPath,
            "--codex-command",
            fakeCodex,
            "--timeout-seconds",
            "10");

        Assert.Equal(0, result.ExitCode);
        var report = JsonNode.Parse(File.ReadAllText(reportPath))!;
        Assert.Equal("pass", report["status"]!.GetValue<string>());
        Assert.Equal(1, report["caseCount"]!.GetValue<int>());
        Assert.True(report["cases"]![0]!["structureQualityScore"]!.GetValue<int>() > 0);
        Assert.True(File.Exists(markdownPath));
        Assert.True(File.Exists(Path.Combine(directory, "workspaces", "synthetic", "reports", "structure-review.md")));
    }

    [Fact]
    public void IntakeDocx_ShouldValidateDraft()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);

        RunIntake(workspace, input);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;

        Assert.True(report["thesisDocumentDraftValid"]!.GetValue<bool>());
    }

    [Fact]
    public void IntakeDocx_ShouldProduceIntakeReport()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);

        RunIntake(workspace, input);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;

        Assert.Equal("pass", report["extractionStatus"]!.GetValue<string>());
        Assert.Equal("pass", report["structuringStatus"]!.GetValue<string>());
        Assert.NotEqual("fail", report["draftContentPreservationStatus"]!.GetValue<string>());
    }

    [Fact]
    public void IntakeDocx_ShouldGenerateFormatCandidateArtifacts()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);
        var candidateFormatPath = Path.Combine(workspace, "structured", "candidate-format-spec.json");
        var candidateReportPath = Path.Combine(workspace, "structured", "format-candidate-report.json");
        var candidateMarkdownPath = Path.Combine(workspace, "structured", "format-candidate-report.md");

        RunIntake(workspace, input);

        Assert.True(File.Exists(candidateFormatPath));
        Assert.True(File.Exists(candidateReportPath));
        Assert.True(File.Exists(candidateMarkdownPath));
        Assert.True(new ThesisSchemaValidator().ValidateFormatFile(candidateFormatPath, SchemaPath("thesis-format-spec.schema.json")).IsValid);
        Assert.True(new ThesisSchemaValidator().ValidateFormatCandidateReportFile(candidateReportPath, SchemaPath("format-candidate-report.schema.json")).IsValid);
        Assert.Contains("DOCX Format Candidate Report", File.ReadAllText(candidateMarkdownPath));
    }

    [Fact]
    public void IntakeDocx_ShouldSummarizeFormatCandidateInReport()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);

        RunIntake(workspace, input);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;
        var reportMarkdown = File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.md"));

        Assert.NotEqual("notRun", report["formatCandidateStatus"]!.GetValue<string>());
        Assert.NotEqual("notRun", report["formatChaosLevel"]!.GetValue<string>());
        Assert.True(report["formatCandidateGeneratedFieldCount"]!.GetValue<int>() > 0);
        Assert.True(report["formatCandidateUnresolvedCount"]!.GetValue<int>() >= 0);
        Assert.Contains("Format candidate", reportMarkdown);
        Assert.Contains("Candidate fields", reportMarkdown);
    }

    [Fact]
    public void IntakeDocx_ShouldSummarizeDraftContentPreservationInReport()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);

        RunIntake(workspace, input);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;
        var mapping = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "structured", "structure-mapping-report.json")))!;
        var reportMarkdown = File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.md"));

        Assert.NotEqual("notRun", report["draftContentPreservationStatus"]!.GetValue<string>());
        Assert.True(report["draftContentMissingSegments"]!.GetValue<int>() >= 0);
        Assert.True(report["draftContentBlockingIssues"]!.GetValue<int>() >= 0);
        Assert.False(string.IsNullOrWhiteSpace(mapping["contentPreservation"]!["sourceContentHash"]!.GetValue<string>()));
        Assert.Contains("Draft content preservation", reportMarkdown);
    }

    [Fact]
    public void IntakeDocx_ShouldNotModifyInputDocx()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);
        var before = SHA256.HashData(File.ReadAllBytes(input));

        RunIntake(workspace, input);
        var after = SHA256.HashData(File.ReadAllBytes(input));

        Assert.Equal(before, after);
    }

    [Fact]
    public void IntakeDocx_ShouldRenderDraftWhenValid()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);

        RunIntake(workspace, input);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;
        var renderedPath = Path.Combine(workspace, "artifacts", "rendered-draft.docx");

        Assert.True(report["renderAttempted"]!.GetValue<bool>());
        Assert.True(File.Exists(renderedPath));
        var validation = new OpenXmlPackageValidator().Validate(renderedPath);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        using var package = WordprocessingDocument.Open(renderedPath, false);
        Assert.Contains(package.MainDocumentPart!.Document.Descendants<W.FootnoteReference>(), reference => reference.Id?.Value == 1);
        Assert.Contains(package.MainDocumentPart.Document.Descendants<W.EndnoteReference>(), reference => reference.Id?.Value == 1);
        Assert.Contains(package.MainDocumentPart.Document.Descendants<W.Hyperlink>(), hyperlink => hyperlink.InnerText == "OpenAI");
        Assert.Contains(package.MainDocumentPart.HyperlinkRelationships, relationship => relationship.Uri.ToString().Contains("example.com", StringComparison.Ordinal));
        Assert.Contains(package.MainDocumentPart.FootnotesPart!.Footnotes!.Descendants<W.Text>(), text => text.Text == "脚注内容");
        Assert.Contains(package.MainDocumentPart.EndnotesPart!.Endnotes!.Descendants<W.Text>(), text => text.Text == "尾注内容");
    }

    [Fact]
    public void IntakeDocx_ShouldRenderTableCellFigures()
    {
        var workspace = NewTempDirectory();
        var input = CreateMediaTableDocx(workspace);

        RunIntake(workspace, input);
        var renderedPath = Path.Combine(workspace, "artifacts", "rendered-draft.docx");

        var validation = new OpenXmlPackageValidator().Validate(renderedPath);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        using var package = WordprocessingDocument.Open(renderedPath, false);
        var table = package.MainDocumentPart!.Document.Descendants<W.Table>().Single(table => table.Descendants<W.Drawing>().Any());
        Assert.Contains(table.Descendants<W.Drawing>(), _ => true);
        Assert.Contains(package.MainDocumentPart.Document.Descendants<A.SourceRectangle>(), crop => crop.Left?.Value == 10000 && crop.Top?.Value == 5000 && crop.Right?.Value == 20000);
        Assert.Contains(table.Descendants<W.TableBorders>(), borders => borders.InsideVerticalBorder?.Val?.Value == W.BorderValues.Single);
        Assert.Contains(table.Descendants<W.TableCellBorders>(), borders => borders.BottomBorder?.Val?.Value == W.BorderValues.Double);
        Assert.Contains(table.Descendants<W.Table>(), nested => nested.Descendants<W.Text>().Any(text => text.Text == "内层"));
    }

    [Fact]
    public void IntakeDocx_ShouldRenderRelationshipFreePreservedTextBoxPassthrough()
    {
        var workspace = NewTempDirectory();
        var input = CreateTextBoxDocx(workspace);

        RunIntake(workspace, input);
        var renderedPath = Path.Combine(workspace, "artifacts", "rendered-draft.docx");

        var validation = new OpenXmlPackageValidator().Validate(renderedPath);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        using var package = WordprocessingDocument.Open(renderedPath, false);
        Assert.Contains(package.MainDocumentPart!.Document.Descendants<W.Picture>(), picture => picture.Descendants<V.TextBox>().Any());
        Assert.Contains(package.MainDocumentPart.Document.Descendants<W.Text>(), text => text.Text == "文本框内容");
    }

    [Fact]
    public void IntakeDocx_ShouldRenderRelationshipBackedChartPassthrough()
    {
        var workspace = NewTempDirectory();
        var input = CreateChartDocx(workspace);

        RunIntake(workspace, input);
        var renderedPath = Path.Combine(workspace, "artifacts", "rendered-draft.docx");

        var validation = new OpenXmlPackageValidator().Validate(renderedPath);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        using var package = WordprocessingDocument.Open(renderedPath, false);
        var chartReference = Assert.Single(package.MainDocumentPart!.Document.Descendants<C.ChartReference>());
        Assert.StartsWith("rIdPreserved", chartReference.Id!.Value);
        Assert.Single(package.MainDocumentPart.ChartParts);
    }

    [Fact]
    public void IntakeDocx_ShouldIncludePrivacyReportArtifact()
    {
        var workspace = NewTempDirectory();
        var input = CreateSyntheticDocx(workspace);

        RunIntake(workspace, input);

        Assert.True(File.Exists(Path.Combine(workspace, "reports", "privacy-scan.json")));
    }

    [Fact]
    public void IntakeDocx_ShouldReportStructuredDiagnosticForMissingInput()
    {
        var workspace = NewTempDirectory();
        var result = RunIntake(workspace, Path.Combine(workspace, "input", "missing.docx"));

        Assert.Equal(2, result.ExitCode);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "reports", "intake-report.json")))!;
        Assert.Equal("1.0.0", report["reportVersion"]!.GetValue<string>());
        var diagnostic = report["diagnostics"]!.AsArray()[0]!;
        Assert.Equal("intake.input.notFound", diagnostic["code"]!.GetValue<string>());
        Assert.Equal("error", diagnostic["severity"]!.GetValue<string>());
        Assert.Equal("$.input", diagnostic["path"]!.GetValue<string>());
    }

    private static CliResult RunIntake(string workspace, string input)
    {
        return CliRunner.Run(RepoRoot(), "intake", "docx", "--input", input, "--workspace", workspace, "--template", TemplatePath());
    }

    private static ThesisStructuringResult MapSynthetic()
    {
        return new ThesisStructureMapper().Map(ExtractSynthetic(), "synthetic-extraction.json");
    }

    private static DocxExtractionResult ExtractSynthetic()
    {
        var directory = NewTempDirectory();
        var docx = CreateSyntheticDocx(directory);
        return new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, ArtifactsDirectory = Path.Combine(directory, "artifacts") });
    }

    private static DocxExtractionResult ExtractMergedTable()
    {
        var directory = NewTempDirectory();
        var docx = CreateMergedTableDocx(directory);
        return new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, ArtifactsDirectory = Path.Combine(directory, "artifacts") });
    }

    private static DocxExtractionResult ExtractChaotic()
    {
        var directory = NewTempDirectory();
        var docx = CreateChaoticDocx(directory);
        return new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, ArtifactsDirectory = Path.Combine(directory, "artifacts") });
    }

    private static DocxExtractionResult FigureExtraction()
    {
        const string caption = "图1 系统架构示意图展示模块关系。";
        return new DocxExtractionResult
        {
            InputFileName = "figure-source.docx",
            PlainText = caption,
            Paragraphs =
            [
                new ExtractedParagraph
                {
                    Id = "paragraph-0",
                    Index = 0,
                    Text = caption,
                    EvidencePath = "paragraphs[0]",
                    PossibleRole = "body"
                }
            ],
            Figures =
            [
                new ExtractedFigure
                {
                    Id = "figure-0",
                    Index = 0,
                    ContentType = "image/png",
                    ArtifactPath = "images/image-0.png",
                    SuggestedCaption = caption,
                    EvidencePath = "paragraphs[0]"
                }
            ]
        };
    }

    private static ThesisDocument WrongChapterDocument()
    {
        return new ThesisDocument
        {
            Metadata = new ThesisMetadata { Title = "结构修复测试", Author = "作者", College = "学院", Major = "专业", StudentId = "20260001", Advisor = "导师", Date = "2026-05-19" },
            Sections =
            [
                new ThesisSection
                {
                    Id = "body",
                    Kind = ThesisSectionKind.Body,
                    Blocks =
                    [
                        new HeadingBlock { Id = "heading-second", Level = 1, Inlines = [new TextInline { Text = "第二章 分析" }] },
                        new ParagraphBlock { Id = "paragraph-third-body", Inlines = [new TextInline { Text = "第三章的正文被错误放在第二章末尾。" }] },
                        new HeadingBlock { Id = "heading-third", Level = 1, Inlines = [new TextInline { Text = "第三章 结果" }] }
                    ]
                }
            ]
        };
    }

    private static string BlockText(BlockNode block)
    {
        return block switch
        {
            HeadingBlock heading => string.Concat(heading.Inlines.OfType<TextInline>().Select(inline => inline.Text)),
            ParagraphBlock paragraph => string.Concat(paragraph.Inlines.OfType<TextInline>().Select(inline => inline.Text)),
            _ => string.Empty
        };
    }

    private static DocxExtractionException AssertExtractionError(DocxExtractionOptions options)
    {
        return Assert.Throws<DocxExtractionException>(() => new DocxExtractionService().Extract(options));
    }

    private static string ExtractSyntheticToFile(string directory)
    {
        var docx = CreateSyntheticDocx(directory);
        var extractionPath = Path.Combine(directory, "extraction.json");
        new DocxExtractionService().Extract(new DocxExtractionOptions { InputPath = docx, OutputJsonPath = extractionPath, ArtifactsDirectory = Path.Combine(directory, "artifacts") });
        return extractionPath;
    }

    private static string CreateFakeCodexCommand(string directory)
    {
        var path = Path.Combine(directory, "fake-codex");
        File.WriteAllText(path, """
        #!/bin/sh
        set -eu
        workspace=""
        last_message=""
        while [ "$#" -gt 0 ]; do
          key="$1"
          shift
          case "$key" in
            --cd)
              workspace="$1"
              shift
              ;;
            --output-last-message)
              last_message="$1"
              shift
              ;;
          esac
        done
        cat >/dev/null
        mkdir -p "$workspace/reports"
        printf 'fake codex invoked\n' > "$workspace/reports/fake-codex-marker.txt"
        if [ -n "$last_message" ]; then
          cat > "$last_message" <<'JSON'
        {
          "planVersion": "1.0.0",
          "summary": "No evidence-backed repair needed for this fixture.",
          "operations": [],
          "reviewerNotes": ["fake codex review completed"]
        }
        JSON
        fi
        exit 0
        """);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
        }

        return path;
    }

    private static string CreateSyntheticDocx(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "synthetic.docx");
        using var doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        AddStyles(main);
        AddNumbering(main);
        AddNotes(main);
        var body = main.Document.Body!;
        body.Append(
            Paragraph("合成论文题目", styleId: "Title"),
            Paragraph("摘要", styleId: "Heading1"),
            Paragraph("中文摘要正文，保留原文内容。"),
            Paragraph("关键词：表演；生活化；现实主义"),
            Paragraph("ABSTRACT", styleId: "Heading1"),
            Paragraph("English abstract text."),
            Paragraph("Key words: acting; realism"),
            new W.Paragraph(new W.SimpleField { Instruction = "TOC \\o \"1-3\" \\h" }),
            Paragraph("引言", styleId: "Heading1"),
            ParagraphWithRuns(),
            NumberedParagraph("1.1 研究背景"),
            ParagraphWithFootnoteAndEndnote(),
            HyperlinkParagraph(main),
            Table(),
            Paragraph("参考文献", styleId: "Heading1"),
            Paragraph("[1] 张三. 合成文献[M]. 北京: 示例出版社, 2026."),
            Paragraph("致谢", styleId: "Heading1"),
            Paragraph("感谢所有帮助。"),
            new W.Paragraph(new W.SimpleField { Instruction = "PAGE" }),
            new W.Paragraph(new W.SimpleField { Instruction = "REF bmIntro \\h" }),
            new W.Paragraph(new W.BookmarkStart { Name = "bmIntro", Id = "9" }, new W.Run(new W.Text("书签内容")), new W.BookmarkEnd { Id = "9" }),
            new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }, new W.PageMargin { Top = 1440, Bottom = 1440, Left = 1440, Right = 1440 }));
        main.Document.Save();
        return path;
    }

    private static string CreateMergedTableDocx(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "merged-table.docx");
        using var doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        AddStyles(main);
        main.Document.Body!.Append(
            new W.Table(
                new W.TableRow(
                    new W.TableCell(
                        new W.TableCellProperties(
                            new W.GridSpan { Val = 2 },
                            new W.VerticalMerge { Val = W.MergedCellValues.Restart }),
                        new W.Paragraph(new W.Run(new W.Text("合并项")))),
                    new W.TableCell(new W.Paragraph(new W.Run(new W.Text("说明"))))),
                new W.TableRow(
                    new W.TableCell(
                        new W.TableCellProperties(
                            new W.GridSpan { Val = 2 },
                            new W.VerticalMerge()),
                        new W.Paragraph()),
                    new W.TableCell(new W.Paragraph(new W.Run(new W.Text("延续说明")))))),
            new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }));
        main.Document.Save();
        return path;
    }

    private static string CreateMediaTableDocx(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "media-table.docx");
        using var doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        AddStyles(main);
        var table = new W.Table(
            new W.TableProperties(
                new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
                new W.TableJustification { Val = W.TableRowAlignmentValues.Center },
                new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 8U, Color = "111111" },
                    new W.LeftBorder { Val = W.BorderValues.Single, Size = 4U, Color = "222222" },
                    new W.BottomBorder { Val = W.BorderValues.Single, Size = 8U, Color = "333333" },
                    new W.RightBorder { Val = W.BorderValues.Single, Size = 4U, Color = "444444" },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4U, Color = "555555" },
                    new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4U, Color = "666666" })),
            new W.TableRow(
                new W.TableRowProperties(new W.TableHeader()),
                new W.TableCell(
                    new W.TableCellProperties(
                        new W.TableCellWidth { Type = W.TableWidthUnitValues.Dxa, Width = "2400" },
                        new W.TableCellVerticalAlignment { Val = W.TableVerticalAlignmentValues.Center },
                        new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "D9EAF7" },
                        new W.TableCellBorders(new W.BottomBorder { Val = W.BorderValues.Double, Size = 8U, Color = "AA0000" })),
                    Paragraph("变量")),
                new W.TableCell(
                    new W.TableCellProperties(new W.TableCellWidth { Type = W.TableWidthUnitValues.Dxa, Width = "2400" }),
                    Paragraph("值"))),
            new W.TableRow(
                new W.TableCell(Paragraph("A")),
                new W.TableCell(
                    ImageParagraph(main, 2.4, 1.2, 2),
                    new W.Table(
                        new W.TableProperties(new W.TableBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 4U, Color = "000000" })),
                        new W.TableRow(new W.TableCell(Paragraph("内层")))))));

        main.Document.Body!.Append(
            Paragraph("前置正文"),
            ImageParagraph(main, 3.2, 1.8, 1, (10, 5, 20, 0)),
            Paragraph("图1 系统架构示意图"),
            Paragraph("表1 数据表"),
            table,
            Paragraph("后续正文"),
            new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }));
        main.Document.Save();
        return path;
    }

    private static W.Paragraph ImageParagraph(MainDocumentPart main, double widthCm, double heightCm, uint drawingId, (double Left, double Top, double Right, double Bottom)? crop = null)
    {
        var imagePart = main.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(Convert.FromBase64String(TinyPngBase64)))
        {
            imagePart.FeedData(stream);
        }

        var relationshipId = main.GetIdOfPart(imagePart);
        var widthEmu = UnitConverter.CentimetersToEmu(widthCm);
        var heightEmu = UnitConverter.CentimetersToEmu(heightCm);
        var blipFill = new PIC.BlipFill(new A.Blip { Embed = relationshipId });
        if (crop.HasValue)
        {
            blipFill.AppendChild(new A.SourceRectangle
            {
                Left = (int)Math.Round(crop.Value.Left * 1000),
                Top = (int)Math.Round(crop.Value.Top * 1000),
                Right = (int)Math.Round(crop.Value.Right * 1000),
                Bottom = (int)Math.Round(crop.Value.Bottom * 1000)
            });
        }

        blipFill.AppendChild(new A.Stretch(new A.FillRectangle()));
        return new W.Paragraph(
            new W.Run(
                new W.Drawing(
                    new WP.Inline(
                        new WP.Extent { Cx = widthEmu, Cy = heightEmu },
                        new WP.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                        new WP.DocProperties { Id = drawingId, Name = $"Picture {drawingId}" },
                        new WP.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                        new A.Graphic(
                            new A.GraphicData(
                                new PIC.Picture(
                                    new PIC.NonVisualPictureProperties(
                                        new PIC.NonVisualDrawingProperties { Id = drawingId, Name = $"image-{drawingId}.png" },
                                        new PIC.NonVisualPictureDrawingProperties()),
                                    blipFill,
                                    new PIC.ShapeProperties(
                                        new A.Transform2D(
                                            new A.Offset { X = 0L, Y = 0L },
                                            new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })))));
    }

    private static string CreateTextBoxDocx(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "textbox.docx");
        using var doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        main.Document.Body!.Append(
            new W.Paragraph(
                new W.Run(
                    new W.Picture(
                        new V.Shape(
                            new V.TextBox(
                                new W.TextBoxContent(
                                    new W.Paragraph(new W.Run(new W.Text("文本框内容"))))))))),
            new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }));
        main.Document.Save();
        return path;
    }

    private static string CreateChartDocx(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "chart.docx");
        using var doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        var chartPart = main.AddNewPart<ChartPart>("rIdChartSource");
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"
                          xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <c:lang val="zh-CN"/>
              <c:chart>
                <c:plotArea>
                  <c:layout/>
                  <c:barChart>
                    <c:barDir val="col"/>
                    <c:grouping val="clustered"/>
                    <c:ser>
                      <c:idx val="0"/>
                      <c:order val="0"/>
                      <c:tx><c:v>系列</c:v></c:tx>
                      <c:cat><c:strLit><c:ptCount val="1"/><c:pt idx="0"><c:v>A</c:v></c:pt></c:strLit></c:cat>
                      <c:val><c:numLit><c:formatCode>General</c:formatCode><c:ptCount val="1"/><c:pt idx="0"><c:v>1</c:v></c:pt></c:numLit></c:val>
                    </c:ser>
                    <c:axId val="123456"/>
                    <c:axId val="123457"/>
                  </c:barChart>
                  <c:catAx><c:axId val="123456"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:axPos val="b"/><c:crossAx val="123457"/></c:catAx>
                  <c:valAx><c:axId val="123457"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:axPos val="l"/><c:crossAx val="123456"/></c:valAx>
                </c:plotArea>
              </c:chart>
            </c:chartSpace>
            """)))
        {
            chartPart.FeedData(stream);
        }

        main.Document.Body!.Append(
            new W.Paragraph(new W.Run(ChartDrawing("rIdChartSource"))),
            new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }));
        main.Document.Save();
        return path;
    }

    private static W.Drawing ChartDrawing(string relationshipId)
    {
        var widthEmu = UnitConverter.CentimetersToEmu(6);
        var heightEmu = UnitConverter.CentimetersToEmu(4);
        return new W.Drawing(
            new WP.Inline(
                new WP.Extent { Cx = widthEmu, Cy = heightEmu },
                new WP.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new WP.DocProperties { Id = 10U, Name = "Chart 1" },
                new WP.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(new C.ChartReference { Id = relationshipId })
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart"
                    })));
    }

    private static string CreateChaoticDocx(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "chaotic.docx");
        using var doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body());
        AddStyles(main);
        var body = main.Document.Body!;
        body.Append(DirectHeading("第一章 绪论"));
        for (var i = 0; i < 12; i++)
        {
            body.Append(DirectBodyParagraph($"混乱正文段落 {i + 1}，内容需要保留但格式不应直接成为模板。", (360 + i).ToString()));
        }

        body.Append(DirectHeading("第二章 分析"));
        body.Append(new W.Paragraph());
        body.Append(new W.Paragraph());
        body.Append(new W.SectionProperties(new W.PageSize { Width = 11906, Height = 16838 }));
        main.Document.Save();
        return path;
    }

    private static void ReplaceZipEntry(string packagePath, string entryName, string content)
    {
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update);
        archive.GetEntry(entryName)?.Delete();
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new W.Styles(
            new W.DocDefaults(
                new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(
                    new W.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                    new W.FontSize { Val = "24" })),
                new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(
                    new W.SpacingBetweenLines { Line = "360", LineRule = W.LineSpacingRuleValues.Auto }))),
            new W.Style(
                new W.StyleName { Val = "Normal" },
                new W.StyleRunProperties(new W.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" }, new W.FontSize { Val = "24" }),
                new W.StyleParagraphProperties(new W.SpacingBetweenLines { Line = "360", LineRule = W.LineSpacingRuleValues.Auto }))
            { Type = W.StyleValues.Paragraph, StyleId = "Normal" },
            new W.Style(
                new W.StyleName { Val = "Title" },
                new W.BasedOn { Val = "Normal" },
                new W.StyleRunProperties(new W.Bold(), new W.FontSize { Val = "36" }))
            { Type = W.StyleValues.Paragraph, StyleId = "Title" },
            new W.Style(
                new W.StyleName { Val = "heading 1" },
                new W.BasedOn { Val = "Normal" },
                new W.StyleRunProperties(new W.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "黑体" }, new W.Bold(), new W.FontSize { Val = "32" }),
                new W.StyleParagraphProperties(new W.OutlineLevel { Val = 0 }, new W.SpacingBetweenLines { Line = "360", LineRule = W.LineSpacingRuleValues.Auto }))
            { Type = W.StyleValues.Paragraph, StyleId = "Heading1" });
        styles.Styles.Save();
    }

    private static void AddNumbering(MainDocumentPart main)
    {
        var numberingPart = main.AddNewPart<NumberingDefinitionsPart>();
        numberingPart.Numbering = new W.Numbering(
            new W.AbstractNum(
                new W.Level(
                    new W.StartNumberingValue { Val = 1 },
                    new W.NumberingFormat { Val = W.NumberFormatValues.Decimal },
                    new W.LevelText { Val = "%1." },
                    new W.PreviousParagraphProperties(new W.Indentation { Left = "720", Hanging = "360" }))
                { LevelIndex = 0 })
            { AbstractNumberId = 1 },
            new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });
        numberingPart.Numbering.Save();
    }

    private static void AddNotes(MainDocumentPart main)
    {
        var footnotesPart = main.AddNewPart<FootnotesPart>();
        footnotesPart.Footnotes = new W.Footnotes(new W.Footnote(new W.Paragraph(new W.Run(new W.Text("脚注内容")))) { Id = 2 });
        var endnotesPart = main.AddNewPart<EndnotesPart>();
        endnotesPart.Endnotes = new W.Endnotes(new W.Endnote(new W.Paragraph(new W.Run(new W.Text("尾注内容")))) { Id = 3 });
    }

    private static W.Paragraph Paragraph(string text, string? styleId = null, int? outlineLevel = null)
    {
        var paragraph = new W.Paragraph();
        var pPr = new W.ParagraphProperties();
        if (styleId is not null) pPr.Append(new W.ParagraphStyleId { Val = styleId });
        if (outlineLevel is not null) pPr.Append(new W.OutlineLevel { Val = outlineLevel });
        if (styleId is not null || outlineLevel is not null) paragraph.Append(pPr);
        paragraph.Append(new W.Run(new W.Text(text)));
        return paragraph;
    }

    private static W.Paragraph NumberedParagraph(string text)
    {
        return new W.Paragraph(
            new W.ParagraphProperties(new W.NumberingProperties(new W.NumberingLevelReference { Val = 0 }, new W.NumberingId { Val = 1 })),
            new W.Run(new W.Text(text)));
    }

    private static W.Paragraph ParagraphWithRuns()
    {
        return new W.Paragraph(
            new W.Run(new W.RunProperties(new W.Bold()), new W.Text("加粗")),
            new W.Run(new W.RunProperties(new W.Italic()), new W.Text("斜体")),
            new W.Run(new W.RunProperties(new W.Underline { Val = W.UnderlineValues.Single }), new W.Text("下划线")),
            new W.Run(new W.RunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }), new W.Text("上标")),
            new W.Run(new W.RunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Subscript }), new W.Text("下标")));
    }

    private static W.Paragraph ParagraphWithFootnoteAndEndnote()
    {
        return new W.Paragraph(new W.Run(new W.Text("正文引用")), new W.Run(new W.FootnoteReference { Id = 2 }), new W.Run(new W.EndnoteReference { Id = 3 }));
    }

    private static W.Paragraph HyperlinkParagraph(MainDocumentPart main)
    {
        var rel = main.AddHyperlinkRelationship(new Uri("https://example.com", UriKind.Absolute), true);
        return new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("OpenAI"))) { Id = rel.Id });
    }

    private static W.Paragraph DirectHeading(string text)
    {
        return new W.Paragraph(
            new W.ParagraphProperties(
                new W.Justification { Val = W.JustificationValues.Center },
                new W.SpacingBetweenLines { Before = "240", After = "120", Line = "360", LineRule = W.LineSpacingRuleValues.Auto }),
            new W.Run(
                new W.RunProperties(new W.Bold(), new W.FontSize { Val = "32" }),
                new W.Text(text)));
    }

    private static W.Paragraph DirectBodyParagraph(string text, string lineSpacing)
    {
        return new W.Paragraph(
            new W.ParagraphProperties(
                new W.SpacingBetweenLines { Line = lineSpacing, LineRule = W.LineSpacingRuleValues.Auto },
                new W.Indentation { FirstLine = "480" }),
            new W.Run(new W.Text(text)));
    }

    private static W.Table Table()
    {
        return new W.Table(
            new W.TableProperties(new W.TableBorders(new W.TopBorder { Val = W.BorderValues.Single }, new W.BottomBorder { Val = W.BorderValues.Single })),
            new W.TableRow(new W.TableCell(new W.Paragraph(new W.Run(new W.Text("变量")))), new W.TableCell(new W.Paragraph(new W.Run(new W.Text("值"))))),
            new W.TableRow(new W.TableCell(new W.Paragraph(new W.Run(new W.Text("A")))), new W.TableCell(new W.Paragraph(new W.Run(new W.Text("1"))))));
    }

    private static string RepoRoot() => TestRenderHelper.LocateRepoRootForTests();
    private static string SchemaPath(string name) => Path.Combine(RepoRoot(), "schemas", name);
    private static string TemplatePath() => Path.Combine(RepoRoot(), "examples", "templates", "example-university-engineering");

    private static string NewTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
