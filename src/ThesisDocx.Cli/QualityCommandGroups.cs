using System.Globalization;
using System.Text.Json;
using ThesisDocx.Core.Ci;
using ThesisDocx.Core.Models.Requirements;
using ThesisDocx.Core.Requirements;
using ThesisDocx.Core.Services;
using ThesisDocx.Core.Templates.Baselines;
using ThesisDocx.Core.Utilities;

internal static partial class ThesisDocxCli
{
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
        var manifest = options.ContainsKey("fixtures")
            ? new TemplateBaselineManager().InitFixtures(Required(options, "fixtures"), Required(options, "out"))
            : new TemplateBaselineManager().Init(Required(options, "suite"), Required(options, "out"));
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
}
