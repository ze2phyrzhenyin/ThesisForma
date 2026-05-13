using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Packaging;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Structuring;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.ContentPreservation;
using ThesisDocx.Tests.Fixtures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Tests;

public sealed class DocxIntakeStructuringTests
{
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

        Assert.Contains(result.Hyperlinks, h => h.Text == "OpenAI" && h.Uri!.Contains("example.com", StringComparison.Ordinal));
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

        Assert.True(report["renderAttempted"]!.GetValue<bool>());
        Assert.True(File.Exists(Path.Combine(workspace, "artifacts", "rendered-draft.docx")));
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
