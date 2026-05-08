using System.Globalization;
using System.Text.Json;
using ThesisDocx.Core.Diff;
using ThesisDocx.Core.Diff.Layout;
using ThesisDocx.Core.Extraction;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.Ci;
using ThesisDocx.Core.Diagnostics;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Models.Templates;
using ThesisDocx.Core.Onboarding;
using ThesisDocx.Core.Onboarding.Packaging;
using ThesisDocx.Core.Onboarding.Reports;
using ThesisDocx.Core.Privacy;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Rendering;
using ThesisDocx.Core.Services;
using ThesisDocx.Core.Structuring;
using ThesisDocx.Core.Templates;
using ThesisDocx.Core.Templates.Authoring;
using ThesisDocx.Core.Templates.Baselines;
using ThesisDocx.Core.Templates.Gate;
using ThesisDocx.Core.Templates.Regression;
using ThesisDocx.Core.Testing.NegativeFixtures;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using ThesisDocx.Core.Validation.ContentPreservation;
using ThesisDocx.Core.Validation.FormatRuleCoverage;

return ThesisDocxCli.Run(args);

internal static class ThesisDocxCli
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "render" => Render(ParseOptions(args.Skip(1))),
                "validate" => Validate(ParseOptions(args.Skip(1))),
                "validate-input" => ValidateInput(ParseOptions(args.Skip(1))),
                "inspect" => Inspect(ParseOptions(args.Skip(1))),
                "snapshot" => Snapshot(ParseOptions(args.Skip(1))),
                "docx" => Docx(args.Skip(1).ToArray()),
                "template" => Template(args.Skip(1).ToArray()),
                "requirements" => Requirements(args.Skip(1).ToArray()),
                "baseline" => Baseline(args.Skip(1).ToArray()),
                "negative-fixtures" => NegativeFixtures(args.Skip(1).ToArray()),
                "ci" => Ci(args.Skip(1).ToArray()),
                "onboarding" => Onboarding(args.Skip(1).ToArray()),
                "privacy" => Privacy(args.Skip(1).ToArray()),
                "extract" => Extract(args.Skip(1).ToArray()),
                "structure" => Structure(args.Skip(1).ToArray()),
                "intake" => Intake(args.Skip(1).ToArray()),
                "content" => Content(args.Skip(1).ToArray()),
                _ => Fail($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Render(Dictionary<string, string> options)
    {
        var documentPath = Required(options, "document");
        var outputPath = Required(options, "out");
        var hasFormat = options.ContainsKey("format");
        var hasTemplate = options.ContainsKey("template");
        if (hasFormat == hasTemplate)
        {
            return Fail("Pass exactly one of '--format' or '--template'.");
        }

        var document = ReadJson<ThesisDocument>(documentPath);
        ThesisFormatSpec format;
        DocxRenderContext? renderContext = null;
        string formatPathForValidation;
        if (hasTemplate)
        {
            var resolution = new TemplateResolver().Resolve(Required(options, "template"), document, ParseCliVariables(options));
            if (!resolution.IsValid)
            {
                WriteTemplateErrors(resolution.Errors);
                return 2;
            }

            format = resolution.FormatSpec ?? new ThesisFormatSpec();
            formatPathForValidation = WriteTempFormatSpec(format);
            renderContext = CreateRenderContext(resolution);
        }
        else
        {
            formatPathForValidation = Required(options, "format");
            format = ReadJson<ThesisFormatSpec>(formatPathForValidation);
        }

        var schemaRoot = Path.Combine(LocateRepoRoot(), "schemas");
        ResolveRelativeImagePaths(document, Path.GetDirectoryName(Path.GetFullPath(documentPath))!);
        var render = new ThesisRenderService().Render(new RenderRequest
        {
            Document = document,
            Format = format,
            OutputPath = outputPath,
            BaseDirectory = Path.GetDirectoryName(Path.GetFullPath(documentPath)),
            DocumentPath = documentPath,
            FormatPath = formatPathForValidation,
            DocumentSchemaPath = Path.Combine(schemaRoot, "thesis-document.schema.json"),
            FormatSchemaPath = Path.Combine(schemaRoot, "thesis-format-spec.schema.json"),
            ValidateInput = !options.ContainsKey("skip-input-validation"),
            RenderContext = renderContext
        });

        if (!render.Success)
        {
            WriteDiagnostics(render.Diagnostics);
            return 2;
        }

        Console.WriteLine($"Rendered {outputPath}");
        return 0;
    }

    private static int Validate(Dictionary<string, string> options)
    {
        var docxPath = Required(options, "docx");
        var hasFormat = options.ContainsKey("format");
        var hasTemplate = options.ContainsKey("template");
        if (hasFormat == hasTemplate)
        {
            return Fail("Pass exactly one of '--format' or '--template'.");
        }

        var result = new ThesisValidateService().ValidateDocx(new ValidateDocxRequest
        {
            DocxPath = docxPath,
            TemplatePath = hasTemplate ? Required(options, "template") : null,
            Format = hasFormat ? ReadJson<ThesisFormatSpec>(Required(options, "format")) : null
        });

        if (options.ContainsKey("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, ThesisJson.Options));
            return result.IsValid ? 0 : 2;
        }

        if (result.IsValid)
        {
            Console.WriteLine($"Valid ({result.CheckedRules.Count} rules checked)");
            return 0;
        }

        Console.Error.WriteLine($"Invalid ({result.ErrorCount} errors, {result.WarningCount} warnings)");
        WriteDiagnostics(result.Diagnostics);

        return 2;
    }

    private static int ValidateInput(Dictionary<string, string> options)
    {
        var documentPath = Required(options, "document");
        var document = ReadJson<ThesisDocument>(documentPath);
        var hasFormat = options.ContainsKey("format");
        var hasTemplate = options.ContainsKey("template");
        if (hasFormat == hasTemplate)
        {
            return Fail("Pass exactly one of '--format' or '--template'.");
        }

        ThesisFormatSpec format;
        string formatPathForValidation;
        if (hasTemplate)
        {
            var resolution = new TemplateResolver().Resolve(Required(options, "template"), document, ParseCliVariables(options));
            if (!resolution.IsValid)
            {
                WriteTemplateErrors(resolution.Errors);
                return 2;
            }

            format = resolution.FormatSpec ?? new ThesisFormatSpec();
            formatPathForValidation = WriteTempFormatSpec(format);
        }
        else
        {
            formatPathForValidation = Required(options, "format");
            format = ReadJson<ThesisFormatSpec>(formatPathForValidation);
        }

        var schemaRoot = Path.Combine(LocateRepoRoot(), "schemas");
        var result = new ThesisValidateService().ValidateInput(new ValidateInputRequest
        {
            Document = document,
            Format = format,
            BaseDirectory = Path.GetDirectoryName(Path.GetFullPath(documentPath)),
            DocumentPath = documentPath,
            FormatPath = formatPathForValidation,
            DocumentSchemaPath = Path.Combine(schemaRoot, "thesis-document.schema.json"),
            FormatSchemaPath = Path.Combine(schemaRoot, "thesis-format-spec.schema.json")
        });

        if (options.ContainsKey("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, ThesisJson.Options));
            return result.IsValid ? 0 : 2;
        }

        if (result.IsValid)
        {
            Console.WriteLine("Input valid");
            return 0;
        }

        WriteDiagnostics(result.Diagnostics);
        return 2;
    }

    private static int Inspect(Dictionary<string, string> options)
    {
        var docxPath = Required(options, "docx");
        var outputPath = options.GetValueOrDefault("out");
        var result = new DocxInspector().Inspect(docxPath);
        var json = JsonSerializer.Serialize(result, ThesisJson.Options);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(json);
        }
        else
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, json);
            Console.WriteLine($"Wrote {outputPath}");
        }

        return 0;
    }

    private static int Snapshot(Dictionary<string, string> options)
    {
        var docxPath = Required(options, "docx");
        var outputPath = options.GetValueOrDefault("out");
        var snapshot = new DocxSnapshotNormalizer().NormalizeToStableSnapshot(docxPath);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Write(snapshot);
        }
        else
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, snapshot);
            Console.WriteLine($"Wrote {outputPath}");
        }

        return 0;
    }

    private static int Template(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing template subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "list" => TemplateList(options),
            "inspect" => TemplateInspect(options),
            "validate" => TemplateValidate(options),
            "resolve" => TemplateResolve(options),
            "diff" => TemplateDiff(options),
            "coverage" => TemplateCoverage(options),
            "regression" => TemplateRegression(options),
            "gate" => TemplateGate(options),
            "diagnose" => TemplateDiagnose(options),
            "authoring-report" => TemplateAuthoringReport(options),
            _ => Fail($"Unknown template command '{command}'.")
        };
    }

    private static int TemplateList(Dictionary<string, string> options)
    {
        var templates = new TemplateRegistry().ListTemplates(Required(options, "templates"));
        foreach (var template in templates)
        {
            Console.WriteLine($"{template.Id}\t{template.Version}\t{template.Name}");
        }

        return 0;
    }

    private static int TemplateInspect(Dictionary<string, string> options)
    {
        var resolution = new TemplateResolver().Resolve(Required(options, "template"));
        var summary = new
        {
            resolution.Template?.Id,
            resolution.Template?.Name,
            resolution.Template?.Version,
            resolution.Template?.School,
            resolution.Template?.College,
            variables = resolution.Variables,
            assets = resolution.Assets.Select(asset => new { asset.Id, asset.Type, asset.ContentType, asset.Required }),
            pageTemplates = resolution.PageTemplates.Select(layout => new { layout.Id, layout.TargetSectionType, layout.InsertPosition }),
            isValid = resolution.IsValid,
            errors = resolution.Errors
        };
        Console.WriteLine(JsonSerializer.Serialize(summary, ThesisJson.Options));
        return resolution.IsValid ? 0 : 2;
    }

    private static int TemplateValidate(Dictionary<string, string> options)
    {
        var root = LocateRepoRoot();
        var serviceResult = new TemplateWorkflowService().Validate(new TemplateValidateRequest
        {
            TemplatePath = Required(options, "template"),
            SchemaPath = Path.Combine(root, "schemas", "template-package.schema.json")
        });
        var result = serviceResult.Validation;
        if (options.ContainsKey("json"))
        {
            if (result is not null)
            {
                WriteJsonOutput(options.GetValueOrDefault("out"), result);
            }
            else
            {
                WriteJsonOutput(options.GetValueOrDefault("out"), serviceResult);
            }

            return serviceResult.Success ? 0 : 2;
        }

        if (serviceResult.Success)
        {
            Console.WriteLine("Template valid");
            return 0;
        }

        if (result is not null)
        {
            WriteInputErrors(result);
        }
        else
        {
            WriteDiagnostics(serviceResult.Diagnostics);
        }

        return 2;
    }

    private static int TemplateResolve(Dictionary<string, string> options)
    {
        var result = new TemplateResolveService().Resolve(new TemplateResolveRequest
        {
            TemplatePath = Required(options, "template")
        });
        if (!result.Success)
        {
            WriteDiagnostics(result.Diagnostics);
            return 2;
        }

        if (options.ContainsKey("json"))
        {
            WriteJsonOutput(options.GetValueOrDefault("out"), result);
            return 0;
        }

        WriteJsonOutput(options.GetValueOrDefault("out"), result.Resolution?.FormatSpec);
        return 0;
    }

    private static int TemplateDiff(Dictionary<string, string> options)
    {
        var diff = new TemplateDiffEngine().Compare(Required(options, "base"), Required(options, "target"));
        if (options.ContainsKey("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(diff, ThesisJson.Options));
        }
        else
        {
            Console.WriteLine(new TemplateDiffEngine().ToHumanReadable(diff));
        }

        return 0;
    }

    private static int TemplateCoverage(Dictionary<string, string> options)
    {
        var result = new TemplateWorkflowService().Coverage(new TemplateCoverageRequest
        {
            TemplatePath = Required(options, "template")
        });
        if (result.Success)
        {
            WriteJsonOutput(options.GetValueOrDefault("out"), result.Coverage);
            return 0;
        }

        if (options.ContainsKey("json") || options.ContainsKey("out"))
        {
            WriteJsonOutput(options.GetValueOrDefault("out"), result);
        }
        else
        {
            WriteDiagnostics(result.Diagnostics);
        }

        return 2;
    }

    private static int TemplateRegression(Dictionary<string, string> options)
    {
        var outPath = Required(options, "out");
        var outputDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? Directory.GetCurrentDirectory(), "template-regression-artifacts");
        var result = new TemplateRegressionRunner().Run(Required(options, "suite"), outputDirectory);
        WriteJsonOutput(outPath, result);
        return result.Passed ? 0 : 2;
    }

    private static int TemplateGate(Dictionary<string, string> options)
    {
        var outPath = Required(options, "out");
        var outputDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? Directory.GetCurrentDirectory(), "template-gate-artifacts");
        var threshold = options.TryGetValue("coverage-threshold", out var rawThreshold)
            && double.TryParse(rawThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0.75;
        var result = new TemplateWorkflowService().Gate(new TemplateGateRequest
        {
            TemplatePath = Required(options, "template"),
            DocumentPath = Required(options, "document"),
            OutputDirectory = outputDirectory,
            CoverageThreshold = threshold
        });
        var report = result.Report;
        if (report is null)
        {
            WriteJsonOutput(outPath, result);
            return 2;
        }

        WriteJsonOutput(outPath, report);
        return report.Status == TemplateGateStatus.Fail ? 2 : 0;
    }

    private static int TemplateDiagnose(Dictionary<string, string> options)
    {
        var outPath = Required(options, "out");
        var outDirectory = Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? Directory.GetCurrentDirectory();
        var artifactDirectory = Path.Combine(outDirectory, "template-diagnostic-artifacts");
        var result = new TemplateWorkflowService().Diagnose(new TemplateDiagnoseRequest
        {
            TemplatePath = Required(options, "template"),
            DocumentPath = Required(options, "document"),
            RequirementsPath = options.GetValueOrDefault("requirements"),
            SuitePath = options.GetValueOrDefault("suite"),
            OutputDirectory = artifactDirectory,
            CoverageThreshold = 0.75
        });
        var report = result.Report;
        if (report is null)
        {
            WriteJsonOutput(outPath, result);
            return 2;
        }

        WriteJsonOutput(outPath, report);
        if (options.TryGetValue("markdown", out var markdownPath))
        {
            WriteTextOutput(markdownPath, new DiagnosticReportMarkdownRenderer().Render(report));
        }

        Console.WriteLine($"Diagnostic status: {report.Status}; issues: {report.IssueCount}; errors: {report.BreakingCount}; warnings: {report.WarningCount}");
        return report.BreakingCount > 0 ? 2 : 0;
    }

    private static int TemplateAuthoringReport(Dictionary<string, string> options)
    {
        var outPath = Required(options, "out");
        var threshold = options.TryGetValue("threshold", out var rawThreshold)
            && double.TryParse(rawThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0.85;
        var result = new TemplateWorkflowService().AuthoringReport(new TemplateAuthoringReportRequest
        {
            TemplatePath = Required(options, "template"),
            DocumentPath = Required(options, "document"),
            RequirementsPath = options.GetValueOrDefault("requirements"),
            SuitePath = options.GetValueOrDefault("suite"),
            OutputDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? Directory.GetCurrentDirectory(), "template-authoring-artifacts"),
            CoverageThreshold = threshold
        });
        var report = result.Report;
        if (report is null)
        {
            WriteJsonOutput(outPath, result);
            return 2;
        }

        WriteJsonOutput(outPath, report);
        if (options.TryGetValue("markdown", out var markdownPath))
        {
            WriteTextOutput(markdownPath, new TemplateAuthoringMarkdownRenderer().Render(report));
        }

        Console.WriteLine($"Authoring readiness: {report.PublishReadiness}; merge decision: {report.SuggestedMergeDecision}; quality score: {report.QualityScore}");
        return report.PublishReadiness == "notReady" ? 2 : 0;
    }

    private static int Requirements(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing requirements subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "validate" => RequirementsValidate(options),
            "report" => RequirementsReport(options),
            _ => Fail($"Unknown requirements command '{command}'.")
        };
    }

    private static int RequirementsValidate(Dictionary<string, string> options)
    {
        var requirementsPath = Required(options, "requirements");
        var schemaPath = Path.Combine(LocateRepoRoot(), "schemas", "requirement-capture.schema.json");
        var serviceResult = new RequirementsWorkflowService().Validate(new RequirementsValidateRequest
        {
            RequirementsPath = requirementsPath,
            SchemaPath = schemaPath
        });
        var result = serviceResult.Validation;
        if (options.ContainsKey("json"))
        {
            if (result is not null)
            {
                WriteJsonOutput(options.GetValueOrDefault("out"), result);
            }
            else
            {
                WriteJsonOutput(options.GetValueOrDefault("out"), serviceResult);
            }
        }
        else if (serviceResult.Success)
        {
            Console.WriteLine("Requirements valid");
        }
        else if (result is not null)
        {
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine(error.ToString());
            }
        }
        else
        {
            WriteDiagnostics(serviceResult.Diagnostics);
        }

        return serviceResult.Success ? 0 : 2;
    }

    private static int RequirementsReport(Dictionary<string, string> options)
    {
        var result = new RequirementsWorkflowService().Report(new RequirementsReportRequest
        {
            RequirementsPath = Required(options, "requirements"),
            TemplatePath = options.GetValueOrDefault("template")
        });
        var report = result.Report;
        if (report is null)
        {
            WriteJsonOutput(Required(options, "out"), result);
            return 2;
        }

        WriteJsonOutput(Required(options, "out"), report);
        return report.IsValid ? 0 : 2;
    }

    private static int Baseline(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing baseline subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "list" => BaselineList(options),
            "init" => BaselineInit(options),
            "compare" => BaselineCompare(options),
            "update" => BaselineUpdate(options),
            _ => Fail($"Unknown baseline command '{command}'.")
        };
    }

    private static int BaselineList(Dictionary<string, string> options)
    {
        var entries = new TemplateBaselineManager().List(Required(options, "suite"));
        foreach (var entry in entries)
        {
            Console.WriteLine($"{entry.CaseId}\t{entry.TemplateId}\t{entry.TemplateVersion}");
        }

        return 0;
    }

    private static int BaselineInit(Dictionary<string, string> options)
    {
        var manifest = new TemplateBaselineManager().Init(Required(options, "suite"), Required(options, "out"));
        WriteJsonOutput(null, new { manifest.SuiteId, baselineCount = manifest.Baselines.Count });
        return 0;
    }

    private static int BaselineCompare(Dictionary<string, string> options)
    {
        var outputPath = Required(options, "out");
        var outputDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Directory.GetCurrentDirectory(), "baseline-compare-artifacts");
        var result = options.ContainsKey("fixtures")
            ? new TemplateBaselineManager().CompareFixtures(Required(options, "fixtures"), outputDirectory)
            : new TemplateBaselineManager().CompareSuite(Required(options, "suite"), outputDirectory);
        WriteJsonOutput(outputPath, result);
        return result.Passed ? 0 : 2;
    }

    private static int BaselineUpdate(Dictionary<string, string> options)
    {
        var outputPath = Required(options, "out");
        var result = new TemplateBaselineManager().Update(
            Required(options, "suite"),
            Required(options, "case"),
            options.GetValueOrDefault("reason") ?? string.Empty,
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Directory.GetCurrentDirectory(), "baseline-update-artifacts"));
        WriteJsonOutput(outputPath, result);
        return result.Errors.Count == 0 ? 0 : 2;
    }

    private static int NegativeFixtures(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing negative-fixtures subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "run" => NegativeFixturesRun(options),
            _ => Fail($"Unknown negative-fixtures command '{command}'.")
        };
    }

    private static int NegativeFixturesRun(Dictionary<string, string> options)
    {
        var serviceResult = new NegativeFixturesWorkflowService().Run(new NegativeFixturesRunRequest
        {
            ManifestPath = Required(options, "manifest")
        });
        var result = serviceResult.Result;
        if (result is null)
        {
            WriteJsonOutput(Required(options, "out"), serviceResult);
            return 2;
        }

        WriteJsonOutput(Required(options, "out"), result);
        Console.WriteLine($"Negative fixtures: {(result.Passed ? "pass" : "fail")} ({result.Cases.Count} cases)");
        return serviceResult.Success ? 0 : 2;
    }

    private static int Ci(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing ci subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "quality-report" => CiQualityReport(options),
            _ => Fail($"Unknown ci command '{command}'.")
        };
    }

    private static int CiQualityReport(Dictionary<string, string> options)
    {
        var outPath = Required(options, "out");
        var threshold = options.TryGetValue("threshold", out var rawThreshold)
            && double.TryParse(rawThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0.85;
        var result = new CiQualityReportService().Build(new CiQualityReportRequest
        {
            TemplatePath = Required(options, "template"),
            DocumentPath = Required(options, "document"),
            RequirementsPath = Required(options, "requirements"),
            SuitePath = Required(options, "suite"),
            NegativeFixturesPath = Required(options, "negative-fixtures"),
            Threshold = threshold,
            OutputDirectory = Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? Directory.GetCurrentDirectory()
        });
        var report = result.Report;
        if (report is null)
        {
            WriteJsonOutput(outPath, result);
            return 2;
        }

        WriteJsonOutput(outPath, report);
        if (options.TryGetValue("markdown", out var markdownPath))
        {
            WriteTextOutput(markdownPath, new CiQualityMarkdownRenderer().Render(report));
        }

        Console.WriteLine($"CI quality status: {report.Status}; merge decision: {report.MergeDecision}; quality score: {report.QualityScore}");
        return report.Status == "fail" ? 2 : 0;
    }

    private static int Extract(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing extract subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "docx" => ExtractDocx(options),
            _ => Fail($"Unknown extract command '{command}'.")
        };
    }

    private static int ExtractDocx(Dictionary<string, string> options)
    {
        var input = options.GetValueOrDefault("input") ?? Required(options, "docx");
        var output = Required(options, "out");
        try
        {
            var result = new DocxExtractionService().Extract(new DocxExtractionOptions
            {
                InputPath = input,
                OutputJsonPath = output,
                PlainTextPath = options.GetValueOrDefault("text"),
                MarkdownPath = options.GetValueOrDefault("markdown"),
                ArtifactsDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)) ?? Directory.GetCurrentDirectory(), "artifacts")
            });
            Console.WriteLine($"Extracted {result.Paragraphs.Count} paragraphs, {result.Tables.Count} tables, {result.Figures.Count} figures");
            return 0;
        }
        catch (DocxExtractionException ex)
        {
            var diagnostic = ExtractionDiagnostic(ex, "DocxExtractionService");
            WriteJsonOutput(output, new { reportVersion = "1.0.0", success = false, diagnostics = new[] { diagnostic } });
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 2;
        }
    }

    private static int Content(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing content subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "audit" => ContentAudit(options),
            _ => Fail($"Unknown content command '{command}'.")
        };
    }

    private static int ContentAudit(Dictionary<string, string> options)
    {
        var sourcePath = Required(options, "source-extraction");
        var renderedPath = options.GetValueOrDefault("rendered-extraction");
        if (string.IsNullOrWhiteSpace(renderedPath))
        {
            if (!options.TryGetValue("rendered-docx", out var renderedDocx))
            {
                return Fail("Pass either '--rendered-extraction' or '--rendered-docx'.");
            }

            var outDirectory = Path.GetDirectoryName(Path.GetFullPath(Required(options, "out"))) ?? Directory.GetCurrentDirectory();
            renderedPath = Path.Combine(outDirectory, "rendered-extraction.json");
            new DocxExtractionService().Extract(new DocxExtractionOptions
            {
                InputPath = renderedDocx,
                OutputJsonPath = renderedPath,
                ArtifactsDirectory = Path.Combine(outDirectory, "artifacts")
            });
        }

        var result = new ContentPreservationAuditor().Audit(sourcePath, renderedPath);
        WriteJsonOutput(Required(options, "out"), result);
        if (options.TryGetValue("markdown", out var markdownPath))
        {
            WriteTextOutput(markdownPath, ContentPreservationAuditor.ToMarkdown(result));
        }

        Console.WriteLine($"Content preservation audit: {result.Status} ({result.MissingSegments.Count} missing segments, {result.BlockingIssues.Count} blocking)");
        return result.Status == "fail" ? 2 : 0;
    }

    private static int Structure(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing structure subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "draft" => StructureDraft(options),
            "prompt" => StructurePrompt(options),
            _ => Fail($"Unknown structure command '{command}'.")
        };
    }

    private static int StructureDraft(Dictionary<string, string> options)
    {
        var extractionPath = Required(options, "extraction");
        var reportPath = Required(options, "report");
        try
        {
            var extraction = ReadJson<DocxExtractionResult>(extractionPath);
            var result = new ThesisStructureMapper().Map(extraction, extractionPath);
            new ThesisStructureMapper().WriteOutputs(result, Required(options, "out"), reportPath, Required(options, "unresolved"), options.GetValueOrDefault("evidence"));
            Console.WriteLine($"Structured draft with {result.Report.RuleBasedMappedCount} mapped items and {result.UnresolvedItems.Count} unresolved items");
            return 0;
        }
        catch (Exception ex)
        {
            var diagnostic = IntakeFailureDiagnostic(
                "intake.structure.failed",
                "$.extraction",
                "Structure draft failed before producing a draft document.",
                "Validate extraction JSON and rerun structure draft inside the private intake workspace.",
                "StructureDraft",
                ex);
            WriteJsonOutput(reportPath, new { reportVersion = "1.0.0", success = false, diagnostics = new[] { diagnostic } });
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 2;
        }
    }

    private static int StructurePrompt(Dictionary<string, string> options)
    {
        var markdown = new StructurePromptBuilder().Build(Required(options, "extraction"));
        WriteTextOutput(Required(options, "out"), markdown);
        return 0;
    }

    private static int Intake(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing intake subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "docx" => IntakeDocx(options),
            _ => Fail($"Unknown intake command '{command}'.")
        };
    }

    private static int IntakeDocx(Dictionary<string, string> options)
    {
        var workspace = Required(options, "workspace");
        var input = Required(options, "input");
        var template = Required(options, "template");
        Directory.CreateDirectory(Path.Combine(workspace, "input"));
        Directory.CreateDirectory(Path.Combine(workspace, "extraction"));
        Directory.CreateDirectory(Path.Combine(workspace, "structured"));
        Directory.CreateDirectory(Path.Combine(workspace, "reports"));
        Directory.CreateDirectory(Path.Combine(workspace, "artifacts"));
        var workspaceReadme = Path.Combine(workspace, "README.md");
        if (!File.Exists(workspaceReadme))
        {
            WriteTextOutput(workspaceReadme, """
            # DOCX Structure Pilot Workspace

            Place the source Word file at `input/input.docx`.
            Generated extraction and structured draft files may contain user thesis content and should stay in this ignored workspace.
            """);
        }

        var report = new IntakeDocxReport { InputDocx = Path.GetFileName(input) };
        var extractionPath = Path.Combine(workspace, "extraction", "extraction.json");
        var plainTextPath = Path.Combine(workspace, "extraction", "plain-text.txt");
        var markdownPath = Path.Combine(workspace, "extraction", "extracted.md");
        var draftPath = Path.Combine(workspace, "structured", "thesis-document.draft.json");
        var mappingPath = Path.Combine(workspace, "structured", "structure-mapping-report.json");
        var unresolvedPath = Path.Combine(workspace, "structured", "unresolved-items.json");
        var evidencePath = Path.Combine(workspace, "structured", "evidence-links.json");
        var promptPath = Path.Combine(workspace, "reports", "structure-codex-prompt.md");
        var reportPath = Path.Combine(workspace, "reports", "intake-report.json");
        var reportMarkdownPath = Path.Combine(workspace, "reports", "intake-report.md");

        if (!File.Exists(input))
        {
            var diagnostic = new UnifiedDiagnostic
            {
                Code = "intake.input.notFound",
                Severity = DiagnosticSeverity.Error,
                Path = "$.input",
                Message = "Input DOCX file does not exist.",
                FixHint = "Copy the uploaded Word file into the workspace input directory and rerun intake.",
                Category = DiagnosticCategory.Intake,
                Source = "IntakeDocx"
            };
            report.ExtractionStatus = "fail";
            report.Diagnostics.Add(diagnostic);
            report.BlockingIssues.Add($"{diagnostic.Code}: {diagnostic.Message}");
            report.RecommendedNextActions = ["Copy the uploaded Word file into the workspace input directory and rerun intake."];
            WriteJsonOutput(reportPath, report);
            WriteTextOutput(reportMarkdownPath, IntakeReportMarkdown(report));
            return 2;
        }

        try
        {
            var extraction = new DocxExtractionService().Extract(new DocxExtractionOptions
            {
                InputPath = input,
                OutputJsonPath = extractionPath,
                PlainTextPath = plainTextPath,
                MarkdownPath = markdownPath,
                ArtifactsDirectory = Path.Combine(workspace, "artifacts"),
                WorkspaceRoot = workspace
            });
            report.ExtractionStatus = "pass";
            report.Artifacts.AddRange([extractionPath, plainTextPath, markdownPath]);
            var privacyReportPath = Path.Combine(workspace, "reports", "privacy-scan.json");
            var privacy = new PrivacyGuard().Scan(new PrivacyGuardOptions { Path = workspace });
            WriteJsonOutput(privacyReportPath, privacy);
            report.Artifacts.Add(privacyReportPath);
            report.Warnings.AddRange(privacy.Findings.Where(f => !UnifiedDiagnosticMapper.IsError(f.Severity)).Select(f => $"{f.Code}: {f.Message}"));
            report.BlockingIssues.AddRange(privacy.Findings.Where(f => UnifiedDiagnosticMapper.IsError(f.Severity)).Select(f => $"{f.Code}: {f.Message}"));

            var structured = new ThesisStructureMapper().Map(extraction, extractionPath);
            new ThesisStructureMapper().WriteOutputs(structured, draftPath, mappingPath, unresolvedPath, evidencePath);
            report.StructuringStatus = "pass";
            report.UnresolvedCount = structured.UnresolvedItems.Count;
            report.Artifacts.AddRange([draftPath, mappingPath, unresolvedPath, evidencePath]);
            WriteTextOutput(promptPath, new StructurePromptBuilder().Build(extractionPath));
            report.Artifacts.Add(promptPath);

            var draft = ReadJson<ThesisDocument>(draftPath);
            var resolution = new TemplateResolver().Resolve(template, draft);
            if (!resolution.IsValid)
            {
                report.BlockingIssues.AddRange(resolution.Errors.Select(e => e.ToString()));
            }
            else
            {
                var format = resolution.FormatSpec ?? new ThesisFormatSpec();
                var tempFormat = WriteTempFormatSpec(format);
                var inputValidation = ValidateInputFiles(draftPath, tempFormat, draft, format);
                report.ThesisDocumentDraftValid = inputValidation.IsValid;
                report.BlockingIssues.AddRange(inputValidation.Errors.Select(e => e.ToString()));
                report.Warnings.AddRange(inputValidation.Warnings.Select(e => e.ToString()));
                if (inputValidation.IsValid)
                {
                    ResolveRelativeImagePaths(draft, Path.GetDirectoryName(Path.GetFullPath(draftPath))!);
                    var renderedPath = Path.Combine(workspace, "artifacts", "rendered-draft.docx");
                    new DocxRenderer().Render(draft, format, renderedPath, CreateRenderContext(resolution));
                    report.RenderAttempted = true;
                    report.Artifacts.Add(renderedPath);
                    var openXml = new OpenXmlPackageValidator().Validate(renderedPath);
                    report.RenderValid = openXml.IsValid;
                    report.BlockingIssues.AddRange(openXml.Errors.Select(e => e.ToString()));
                    var inspectPath = Path.Combine(workspace, "reports", "rendered-draft.inspect.json");
                    WriteJsonOutput(inspectPath, new DocxInspector().Inspect(renderedPath));
                    report.Artifacts.Add(inspectPath);
                }
            }
        }
        catch (DocxExtractionException ex)
        {
            var diagnostic = ExtractionDiagnostic(ex, "IntakeDocx");
            report.ExtractionStatus = "fail";
            report.Diagnostics.Add(diagnostic);
            report.BlockingIssues.Add($"{diagnostic.Code}: {diagnostic.Message}");
        }
        catch (Exception ex)
        {
            var diagnostic = IntakeFailureDiagnostic(
                "intake.docx.failed",
                "$",
                "DOCX intake failed before producing all expected artifacts.",
                "Review intake-report.json, fix the referenced workspace input or template issue, and rerun intake.",
                "IntakeDocx",
                ex);
            if (report.ExtractionStatus == "notRun")
            {
                report.ExtractionStatus = "fail";
            }

            if (report.ExtractionStatus == "pass" && report.StructuringStatus == "notRun")
            {
                report.StructuringStatus = "fail";
            }

            report.Diagnostics.Add(diagnostic);
            report.BlockingIssues.Add($"{diagnostic.Code}: {diagnostic.Message}");
        }

        report.RecommendedNextActions = report.BlockingIssues.Count == 0
            ? ["Review unresolved items and evidence links before using the rendered draft."]
            : ["Open intake-report.json and fix blocking issues before rendering final output."];
        WriteJsonOutput(reportPath, report);
        WriteTextOutput(reportMarkdownPath, IntakeReportMarkdown(report));
        return report.BlockingIssues.Count == 0 ? 0 : 2;
    }

    private static string IntakeReportMarkdown(IntakeDocxReport report)
    {
        return $"""
        # DOCX Intake Report

        - Extraction: `{report.ExtractionStatus}`
        - Structuring: `{report.StructuringStatus}`
        - Draft valid: `{report.ThesisDocumentDraftValid}`
        - Render attempted: `{report.RenderAttempted}`
        - Render valid: `{report.RenderValid}`
        - Unresolved items: `{report.UnresolvedCount}`

        ## Blocking Issues
        {(report.BlockingIssues.Count == 0 ? "- None" : string.Join(Environment.NewLine, report.BlockingIssues.Select(i => "- " + i)))}

        ## Next Actions
        {string.Join(Environment.NewLine, report.RecommendedNextActions.Select(a => "- " + a))}
        """;
    }

    private static UnifiedDiagnostic ExtractionDiagnostic(DocxExtractionException ex, string source)
    {
        return new UnifiedDiagnostic
        {
            Code = ex.Code,
            Severity = UnifiedDiagnosticMapper.NormalizeSeverity(ex.Severity),
            Path = ex.Path,
            Message = ex.Message,
            FixHint = ex.FixHint,
            Category = DiagnosticCategory.Intake,
            Source = source
        };
    }

    private static UnifiedDiagnostic IntakeFailureDiagnostic(string code, string path, string message, string fixHint, string source, Exception ex)
    {
        return new UnifiedDiagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Path = path,
            Message = message,
            FixHint = fixHint,
            Category = DiagnosticCategory.Intake,
            Source = source,
            Details = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["exceptionType"] = ex.GetType().Name
            }
        };
    }

    private static int Privacy(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing privacy subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "scan" => PrivacyScan(options),
            _ => Fail($"Unknown privacy command '{command}'.")
        };
    }

    private static int PrivacyScan(Dictionary<string, string> options)
    {
        var guardOptions = new PrivacyGuardOptions { Path = Required(options, "path") };
        if (options.TryGetValue("max-evidence-excerpt-length", out var maxEvidenceExcerptLength))
        {
            guardOptions.MaxEvidenceExcerptLength = int.Parse(maxEvidenceExcerptLength, CultureInfo.InvariantCulture);
        }

        if (options.TryGetValue("max-base64-length", out var maxBase64Length))
        {
            guardOptions.MaxBase64Length = int.Parse(maxBase64Length, CultureInfo.InvariantCulture);
        }

        if (options.TryGetValue("max-warnings", out var maxWarnings))
        {
            guardOptions.MaxWarningCount = int.Parse(maxWarnings, CultureInfo.InvariantCulture);
        }

        guardOptions.SuppressedWarningCodes = SplitOptionList(options, "suppress-warning-code").ToHashSet(StringComparer.Ordinal);
        guardOptions.SuppressedWarningPathPrefixes = SplitOptionList(options, "suppress-warning-path").ToHashSet(StringComparer.Ordinal);

        var serviceResult = new PrivacyWorkflowService().Scan(new PrivacyScanRequest { Options = guardOptions });
        var result = serviceResult.Scan;
        if (result is null)
        {
            WriteJsonOutput(options.GetValueOrDefault("out"), serviceResult);
            return 2;
        }

        WriteJsonOutput(options.GetValueOrDefault("out"), result);
        Console.WriteLine($"Privacy scan: {(result.IsValid ? "pass" : "fail")} ({result.BreakingCount} errors, {result.WarningCount} warnings, {result.SuppressedWarningCount} suppressed)");
        return serviceResult.Success ? 0 : 2;
    }

    private static int Onboarding(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing onboarding subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "init" => OnboardingInit(options),
            "inspect" => OnboardingInspect(options),
            "validate" => OnboardingValidate(options),
            "scaffold-requirements" => OnboardingScaffoldRequirements(options),
            "scaffold-template" => OnboardingScaffoldTemplate(options),
            "scaffold-fixtures" => OnboardingScaffoldFixtures(options),
            "baseline-init" => OnboardingBaselineInit(options),
            "run-gate" => OnboardingRunGate(options),
            "diagnose" => OnboardingDiagnose(options),
            "authoring-report" => OnboardingAuthoringReport(options),
            "summary" => OnboardingSummary(options),
            "package" => OnboardingPackage(options),
            "package-validate" => OnboardingPackageValidate(options),
            _ => Fail($"Unknown onboarding command '{command}'.")
        };
    }

    private static int OnboardingInit(Dictionary<string, string> options)
    {
        var manifest = new OnboardingWorkspaceInitializer().Initialize(new OnboardingWorkspaceInitOptions
        {
            WorkspacePath = Required(options, "workspace"),
            School = Required(options, "school"),
            College = Required(options, "college"),
            DegreeType = Required(options, "degree-type"),
            Locale = options.GetValueOrDefault("locale") ?? "zh-CN",
            IsRealInstitution = options.ContainsKey("real-institution"),
            Force = options.ContainsKey("force")
        });
        WriteJsonOutput(options.GetValueOrDefault("out"), manifest);
        return 0;
    }

    private static int OnboardingInspect(Dictionary<string, string> options)
    {
        var inspection = new OnboardingWorkspaceInspector().Inspect(Required(options, "workspace"));
        WriteJsonOutput(options.GetValueOrDefault("out"), inspection);
        return inspection.IsValid ? 0 : 2;
    }

    private static int OnboardingValidate(Dictionary<string, string> options)
    {
        var result = new OnboardingWorkspaceValidator().Validate(Required(options, "workspace"));
        if (options.ContainsKey("json") || options.ContainsKey("out"))
        {
            WriteJsonOutput(options.GetValueOrDefault("out"), result);
        }
        else if (result.IsValid)
        {
            Console.WriteLine("Onboarding workspace valid");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine(error);
            }
        }

        return result.IsValid ? 0 : 2;
    }

    private static int OnboardingScaffoldRequirements(Dictionary<string, string> options)
    {
        new OnboardingWorkspaceInitializer().ScaffoldRequirements(Required(options, "workspace"), options.ContainsKey("force"));
        Console.WriteLine("RequirementCapture draft scaffolded");
        return 0;
    }

    private static int OnboardingScaffoldTemplate(Dictionary<string, string> options)
    {
        new OnboardingWorkspaceInitializer().ScaffoldTemplate(Required(options, "workspace"), Required(options, "base-template"), options.ContainsKey("force"));
        Console.WriteLine("Template scaffolded");
        return 0;
    }

    private static int OnboardingScaffoldFixtures(Dictionary<string, string> options)
    {
        new OnboardingWorkspaceInitializer().ScaffoldFixtures(Required(options, "workspace"), Required(options, "document"), options.ContainsKey("force"));
        Console.WriteLine("Fixtures scaffolded");
        return 0;
    }

    private static int OnboardingBaselineInit(Dictionary<string, string> options)
    {
        new OnboardingWorkspaceInitializer().InitializeBaselines(Required(options, "workspace"), Required(options, "reason"), options.ContainsKey("force"));
        Console.WriteLine("Onboarding baselines initialized");
        return 0;
    }

    private static int OnboardingRunGate(Dictionary<string, string> options)
    {
        var workspace = OnboardingWorkspaceInspector.Load(Required(options, "workspace"));
        var result = new TemplateWorkflowService().Gate(new TemplateGateRequest
        {
            TemplatePath = workspace.TemplateDirectory,
            DocumentPath = workspace.DocumentPath,
            OutputDirectory = Path.Combine(workspace.ArtifactsDirectory, "gate"),
            CoverageThreshold = workspace.Manifest.Quality.CoverageThreshold
        });
        var report = result.Report;
        if (report is null)
        {
            WriteJsonOutput(Required(options, "out"), result);
            return 2;
        }

        WriteJsonOutput(Required(options, "out"), report);
        return report.Status == TemplateGateStatus.Fail ? 2 : 0;
    }

    private static int OnboardingDiagnose(Dictionary<string, string> options)
    {
        var workspace = OnboardingWorkspaceInspector.Load(Required(options, "workspace"));
        var result = new TemplateWorkflowService().Diagnose(new TemplateDiagnoseRequest
        {
            TemplatePath = workspace.TemplateDirectory,
            DocumentPath = workspace.DocumentPath,
            RequirementsPath = File.Exists(workspace.RequirementsFile) ? workspace.RequirementsFile : null,
            OutputDirectory = Path.Combine(workspace.ArtifactsDirectory, "diagnose"),
            CoverageThreshold = workspace.Manifest.Quality.CoverageThreshold
        });
        var report = result.Report;
        if (report is null)
        {
            WriteJsonOutput(Required(options, "out"), result);
            return 2;
        }

        WriteJsonOutput(Required(options, "out"), report);
        if (options.TryGetValue("markdown", out var markdownPath))
        {
            WriteTextOutput(markdownPath, new DiagnosticReportMarkdownRenderer().Render(report));
        }

        return report.BreakingCount > 0 ? 2 : 0;
    }

    private static int OnboardingAuthoringReport(Dictionary<string, string> options)
    {
        var workspace = OnboardingWorkspaceInspector.Load(Required(options, "workspace"));
        var result = new TemplateWorkflowService().AuthoringReport(new TemplateAuthoringReportRequest
        {
            TemplatePath = workspace.TemplateDirectory,
            DocumentPath = workspace.DocumentPath,
            RequirementsPath = File.Exists(workspace.RequirementsFile) ? workspace.RequirementsFile : null,
            OutputDirectory = Path.Combine(workspace.ArtifactsDirectory, "authoring"),
            CoverageThreshold = workspace.Manifest.Quality.CoverageThreshold
        });
        var report = result.Report;
        if (report is null)
        {
            WriteJsonOutput(Required(options, "out"), result);
            return 2;
        }

        WriteJsonOutput(Required(options, "out"), report);
        if (options.TryGetValue("markdown", out var markdownPath))
        {
            WriteTextOutput(markdownPath, new TemplateAuthoringMarkdownRenderer().Render(report));
        }

        return report.PublishReadiness == "notReady" ? 2 : 0;
    }

    private static int OnboardingSummary(Dictionary<string, string> options)
    {
        var report = new OnboardingReportBuilder().Build(new OnboardingReportOptions { WorkspacePath = Required(options, "workspace") });
        WriteJsonOutput(Required(options, "out"), report);
        if (options.TryGetValue("markdown", out var markdownPath))
        {
            WriteTextOutput(markdownPath, new OnboardingMarkdownRenderer().Render(report));
        }

        return report.ReleaseReadiness == "blocked" ? 2 : 0;
    }

    private static int OnboardingPackage(Dictionary<string, string> options)
    {
        var result = new TemplatePilotPackageBuilder().Build(Required(options, "workspace"), Required(options, "out"));
        if (!result.IsValid)
        {
            WriteJsonOutput(null, result);
            return 2;
        }

        Console.WriteLine($"Wrote {result.PackagePath}");
        return 0;
    }

    private static int OnboardingPackageValidate(Dictionary<string, string> options)
    {
        var serviceResult = new OnboardingPackageWorkflowService().Validate(new OnboardingPackageValidateRequest
        {
            PackagePath = Required(options, "package")
        });
        var result = serviceResult.Validation;
        if (result is null)
        {
            WriteJsonOutput(options.GetValueOrDefault("out"), serviceResult);
            return 2;
        }

        WriteJsonOutput(options.GetValueOrDefault("out"), result);
        return serviceResult.Success ? 0 : 2;
    }

    private static int Docx(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("Missing docx subcommand.");
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));
        return command switch
        {
            "diff" => DocxDiff(options),
            "layout-signature" => DocxLayoutSignature(options),
            "layout-compare" => DocxLayoutCompare(options),
            _ => Fail($"Unknown docx command '{command}'.")
        };
    }

    private static int DocxDiff(Dictionary<string, string> options)
    {
        var result = new DocxStructureDiffEngine().Compare(Required(options, "base"), Required(options, "target"));
        if (options.ContainsKey("json"))
        {
            WriteJsonOutput(options.GetValueOrDefault("out"), result);
        }
        else
        {
            var lines = result.IsEqual
                ? "DOCX structures are equal."
                : string.Join(Environment.NewLine, result.Changes.Select(change => $"{change.Severity} {change.Category} {change.Path}: {change.BaseValue ?? "<missing>"} -> {change.TargetValue ?? "<missing>"}"));
            if (options.TryGetValue("out", out var outPath))
            {
                File.WriteAllText(outPath, lines);
                Console.WriteLine($"Wrote {outPath}");
            }
            else
            {
                Console.WriteLine(lines);
            }
        }

        return result.IsEqual ? 0 : 2;
    }

    private static int DocxLayoutSignature(Dictionary<string, string> options)
    {
        var signature = new DocxLayoutSignatureExtractor().Extract(Required(options, "docx"));
        WriteJsonOutput(options.GetValueOrDefault("out"), signature);
        return 0;
    }

    private static int DocxLayoutCompare(Dictionary<string, string> options)
    {
        var threshold = options.TryGetValue("threshold", out var rawThreshold)
            && double.TryParse(rawThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0.99;
        var result = new LayoutSignatureComparer().Compare(
            ReadJson<ThesisDocx.Core.Diff.Layout.DocxLayoutSignature>(Required(options, "base")),
            ReadJson<ThesisDocx.Core.Diff.Layout.DocxLayoutSignature>(Required(options, "target")),
            threshold);
        WriteJsonOutput(options.GetValueOrDefault("out"), result);
        return result.MeetsThreshold ? 0 : 2;
    }

    private static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ThesisJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize {path}.");
    }

    private static ThesisInputValidationResult ValidateInputFiles(string documentPath, string formatPath, ThesisDocument document, ThesisFormatSpec format)
    {
        var result = new ThesisInputValidationResult();
        var schemaRoot = Path.Combine(LocateRepoRoot(), "schemas");
        var schemaValidator = new ThesisSchemaValidator();
        Merge(result, schemaValidator.ValidateDocumentFile(documentPath, Path.Combine(schemaRoot, "thesis-document.schema.json")));
        Merge(result, schemaValidator.ValidateFormatFile(formatPath, Path.Combine(schemaRoot, "thesis-format-spec.schema.json")));
        Merge(result, new ThesisInputValidator().Validate(document, format, Path.GetDirectoryName(Path.GetFullPath(documentPath))));
        return result;
    }

    private static void Merge(ThesisInputValidationResult target, ThesisInputValidationResult source)
    {
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
    }

    private static DocxRenderContext CreateRenderContext(TemplateResolutionResult resolution)
    {
        var variables = resolution.Variables
            .Where(variable => variable.Value is not null)
            .ToDictionary(variable => variable.Name, variable => variable.Value!, StringComparer.Ordinal);
        return new DocxRenderContext
        {
            TemplateId = resolution.Template?.Id,
            TemplateVersion = resolution.Template?.Version,
            TemplateSchool = resolution.Template?.School,
            TemplateCollege = resolution.Template?.College,
            ResolvedFormatSpecVersion = resolution.FormatSpec?.SchemaVersion,
            PageTemplates = resolution.PageTemplates,
            Variables = variables,
            Assets = resolution.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal)
        };
    }

    private static Dictionary<string, string> ParseCliVariables(Dictionary<string, string> options)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in GetOptionValues(options, "var"))
        {
            var split = raw.IndexOf('=', StringComparison.Ordinal);
            if (split <= 0)
            {
                continue;
            }

            values[raw[..split]] = raw[(split + 1)..];
        }

        if (options.TryGetValue("vars", out var varsPath) && File.Exists(varsPath))
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(varsPath), ThesisJson.Options);
            if (json is not null)
            {
                foreach (var pair in json)
                {
                    values[pair.Key] = pair.Value;
                }
            }
        }

        return values;
    }

    private static string WriteTempFormatSpec(ThesisFormatSpec format)
    {
        var path = Path.Combine(Path.GetTempPath(), "ThesisDocx.Cli", $"{Guid.NewGuid():N}.format.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(format, ThesisJson.Options));
        return path;
    }

    private static void WriteJsonOutput<T>(string? outputPath, T value)
    {
        var json = JsonSerializer.Serialize(value, ThesisJson.Options);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(json);
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Wrote {outputPath}");
    }

    private static void WriteTextOutput(string outputPath, string value)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, value);
        Console.WriteLine($"Wrote {outputPath}");
    }

    private static void WriteInputErrors(ThesisInputValidationResult result)
    {
        Console.Error.WriteLine($"Input invalid ({result.Errors.Count} errors)");
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine(error.ToString());
        }
    }

    private static void WriteTemplateErrors(IEnumerable<TemplateIssue> errors)
    {
        Console.Error.WriteLine("Template invalid");
        foreach (var error in errors)
        {
            Console.Error.WriteLine(error.ToString());
        }
    }

    private static void WriteDiagnostics(IEnumerable<UnifiedDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var path = string.IsNullOrWhiteSpace(diagnostic.Path) ? "$" : diagnostic.Path;
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message} [{path}]");
        }
    }

    private static string LocateRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ThesisDocx.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ThesisDocx.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static void ResolveRelativeImagePaths(ThesisDocument document, string baseDirectory)
    {
        foreach (var figure in document.Sections.SelectMany(s => s.Blocks).OfType<FigureBlock>())
        {
            if (!string.IsNullOrWhiteSpace(figure.ImagePath) && !Path.IsPathRooted(figure.ImagePath))
            {
                figure.ImagePath = Path.GetFullPath(Path.Combine(baseDirectory, figure.ImagePath));
            }
        }
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var tokens = args.ToArray();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{token}'.");
            }

            var key = token[2..];
            if (i + 1 >= tokens.Length || tokens[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                AddOption(options, key, "true");
                continue;
            }

            AddOption(options, key, tokens[++i]);
        }

        return options;
    }

    private static void AddOption(Dictionary<string, string> options, string key, string value)
    {
        if (options.TryGetValue(key, out var existing))
        {
            options[key] = existing + "\n" + value;
        }
        else
        {
            options[key] = value;
        }
    }

    private static IEnumerable<string> GetOptionValues(Dictionary<string, string> options, string key)
    {
        return options.TryGetValue(key, out var value)
            ? value.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            : [];
    }

    private static IEnumerable<string> SplitOptionList(Dictionary<string, string> options, string key)
    {
        return GetOptionValues(options, key)
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string Required(Dictionary<string, string> options, string key)
    {
        return options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing required option '--{key}'.");
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        thesis-docx render --document examples/simple-thesis/document.json --format examples/format-specs/basic-cn-thesis.json --out out/simple.docx
        thesis-docx validate-input --document examples/simple-thesis/document.json --format examples/format-specs/basic-cn-thesis.json
        thesis-docx validate --docx out/simple.docx --format examples/format-specs/basic-cn-thesis.json
        thesis-docx inspect --docx out/simple.docx --out out/simple.inspect.json
        thesis-docx snapshot --docx out/simple.docx --out out/simple.snapshot.txt
        thesis-docx docx diff --base out/a.docx --target out/b.docx --json --out out/diff.json
        thesis-docx docx layout-signature --docx out/a.docx --out out/a.layout.json
        thesis-docx docx layout-compare --base out/a.layout.json --target out/b.layout.json --threshold 0.99 --out out/layout-compare.json
        thesis-docx template regression --suite examples/template-regression/template-regression-suite.json --out out/template-regression-report.json
        thesis-docx template gate --template examples/templates/example-university-engineering --document examples/full-thesis/document.json --out out/template-gate-report.json
        thesis-docx requirements validate --requirements examples/requirements/example-engineering-requirements.json
        thesis-docx requirements report --requirements examples/requirements/example-engineering-requirements.json --template examples/templates/example-university-engineering --out out/requirements-report.json
        thesis-docx baseline compare --suite examples/template-regression/template-regression-suite.json --out out/baseline-compare-report.json
        thesis-docx template diagnose --template examples/templates/example-university-engineering --document examples/full-thesis/document.json --requirements examples/requirements/example-engineering-requirements.json --suite examples/template-regression/template-regression-suite.json --out out/template-diagnostic-report.json
        thesis-docx template authoring-report --template examples/templates/example-university-engineering --document examples/full-thesis/document.json --requirements examples/requirements/example-engineering-requirements.json --suite examples/template-regression/template-regression-suite.json --threshold 0.85 --out out/template-authoring-report.json
        thesis-docx negative-fixtures run --manifest examples/negative-fixtures/negative-fixture-manifest.json --out out/negative-fixtures-report.json
        thesis-docx ci quality-report --template examples/templates/example-university-engineering --document examples/full-thesis/document.json --requirements examples/requirements/example-engineering-requirements.json --suite examples/template-regression/template-regression-suite.json --negative-fixtures examples/negative-fixtures/negative-fixture-manifest.json --threshold 0.85 --out out/ci/quality-report.json --markdown out/ci/quality-report.md
        thesis-docx privacy scan --path examples --out out/privacy-scan-examples.json
        thesis-docx onboarding init --workspace onboarding-workspaces/pilot-example --school "Example University" --college "Example Engineering College" --degree-type master --locale zh-CN
        thesis-docx onboarding validate --workspace examples/onboarding/example-engineering-pilot
        thesis-docx onboarding summary --workspace examples/onboarding/example-engineering-pilot --out out/onboarding.summary.json --markdown out/onboarding.summary.md
        thesis-docx onboarding package --workspace examples/onboarding/example-engineering-pilot --out out/example-engineering-pilot.template-pilot.zip
        thesis-docx onboarding package-validate --package out/example-engineering-pilot.template-pilot.zip
        thesis-docx extract docx --input onboarding-workspaces/docx-structure-pilot/input/input.docx --out onboarding-workspaces/docx-structure-pilot/extraction/extraction.json --text onboarding-workspaces/docx-structure-pilot/extraction/plain-text.txt --markdown onboarding-workspaces/docx-structure-pilot/extraction/extracted.md
        thesis-docx structure draft --extraction onboarding-workspaces/docx-structure-pilot/extraction/extraction.json --out onboarding-workspaces/docx-structure-pilot/structured/thesis-document.draft.json --report onboarding-workspaces/docx-structure-pilot/structured/structure-mapping-report.json --unresolved onboarding-workspaces/docx-structure-pilot/structured/unresolved-items.json
        thesis-docx structure prompt --extraction onboarding-workspaces/docx-structure-pilot/extraction/extraction.json --out onboarding-workspaces/docx-structure-pilot/reports/structure-codex-prompt.md
        thesis-docx intake docx --input onboarding-workspaces/docx-structure-pilot/input/input.docx --workspace onboarding-workspaces/docx-structure-pilot --template examples/templates/example-university-engineering
        thesis-docx content audit --source-extraction onboarding-workspaces/docx-structure-pilot/extraction/extraction.json --rendered-extraction out/rendered-extraction.json --out out/content-preservation-audit.json --markdown out/content-preservation-audit.md
        """);
    }
}
