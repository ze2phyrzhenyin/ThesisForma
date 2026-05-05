using System.Diagnostics;

namespace ThesisDocx.Tests;

internal static class CliRunner
{
    public static CliResult Run(string repoRoot, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(Path.Combine(repoRoot, "src", "ThesisDocx.Cli"));
        psi.ArgumentList.Add("--");
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment["NUGET_PACKAGES"] = Path.Combine(repoRoot, ".nuget", "packages");
        psi.Environment["NUGET_HTTP_CACHE_PATH"] = Path.Combine(repoRoot, ".nuget", "http-cache");
        psi.Environment["NUGET_TEMP"] = Path.Combine(repoRoot, ".nuget", "temp");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start dotnet process.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(180000);
        return new CliResult(process.ExitCode, stdout, stderr);
    }
}

internal sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
