using System.Diagnostics;

namespace ThesisDocx.Tests;

internal static class CliRunner
{
    private const int TimeoutMilliseconds = 180000;
    private static readonly object BuildLock = new();
    private static readonly HashSet<string> BuiltCliAssemblies = new(StringComparer.OrdinalIgnoreCase);

    public static CliResult Run(string repoRoot, params string[] args)
    {
        var cliAssembly = EnsureCliBuilt(repoRoot);
        var psi = CreateDotnetProcess(repoRoot);
        psi.ArgumentList.Add(cliAssembly);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        return RunProcess(psi);
    }

    private static string EnsureCliBuilt(string repoRoot)
    {
        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            ? "Release"
            : "Debug";
        var cliAssembly = Path.Combine(repoRoot, "src", "ThesisDocx.Cli", "bin", configuration, "net10.0", "ThesisDocx.Cli.dll");
        if (BuiltCliAssemblies.Contains(cliAssembly) && File.Exists(cliAssembly))
        {
            return cliAssembly;
        }

        lock (BuildLock)
        {
            if (BuiltCliAssemblies.Contains(cliAssembly) && File.Exists(cliAssembly))
            {
                return cliAssembly;
            }

            var psi = CreateDotnetProcess(repoRoot);
            psi.ArgumentList.Add("build");
            psi.ArgumentList.Add(Path.Combine(repoRoot, "src", "ThesisDocx.Cli", "ThesisDocx.Cli.csproj"));
            psi.ArgumentList.Add("--no-restore");
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(configuration);

            var result = RunProcess(psi);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Could not build ThesisDocx.Cli.{Environment.NewLine}{result.StandardOutput}{result.StandardError}");
            }

            BuiltCliAssemblies.Add(cliAssembly);
        }

        return cliAssembly;
    }

    private static ProcessStartInfo CreateDotnetProcess(string repoRoot)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.Environment["NUGET_PACKAGES"] = Path.Combine(repoRoot, ".nuget", "packages");
        psi.Environment["NUGET_HTTP_CACHE_PATH"] = Path.Combine(repoRoot, ".nuget", "http-cache");
        psi.Environment["NUGET_TEMP"] = Path.Combine(repoRoot, ".nuget", "temp");

        return psi;
    }

    private static CliResult RunProcess(ProcessStartInfo psi)
    {
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start dotnet process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Command timed out after {TimeoutMilliseconds} ms: {string.Join(" ", psi.ArgumentList)}");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        return new CliResult(process.ExitCode, stdout, stderr);
    }
}

internal sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
