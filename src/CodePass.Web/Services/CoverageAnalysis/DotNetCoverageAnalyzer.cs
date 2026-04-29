using System.Diagnostics;
using System.Text;

namespace CodePass.Web.Services.CoverageAnalysis;

public sealed class DotNetCoverageAnalyzer : ICoverageAnalyzer
{
    private readonly CoberturaCoverageParser _parser;

    public DotNetCoverageAnalyzer()
        : this(new CoberturaCoverageParser())
    {
    }

    public DotNetCoverageAnalyzer(CoberturaCoverageParser parser)
    {
        _parser = parser;
    }

    public async Task<CoverageAnalysisResult> AnalyzeAsync(
        string solutionPath,
        CancellationToken cancellationToken = default,
        IProgress<CoverageAnalysisProgressDto>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        var resultsDirectory = Path.Combine(Path.GetTempPath(), "codepass-coverage-analysis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(resultsDirectory);

        try
        {
            Report(progress, CoverageAnalysisProgressStage.RunningTests, "Starting dotnet test with XPlat Code Coverage...", percentComplete: 25, detail: solutionPath);
            var output = await RunDotnetCoverageAsync(solutionPath, resultsDirectory, cancellationToken, progress);
            Report(progress, CoverageAnalysisProgressStage.CollectingCoverage, "Finding generated Cobertura coverage files...", percentComplete: 75, detail: resultsDirectory);
            var coberturaFiles = Directory
                .EnumerateFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            if (coberturaFiles.Length == 0)
            {
                throw new InvalidOperationException(
                    "dotnet test completed but no coverage.cobertura.xml files were produced. " +
                    "Ensure target test projects reference coverlet.collector and that tests are discoverable. " +
                    $"dotnet output:{Environment.NewLine}{output}");
            }

            Report(
                progress,
                CoverageAnalysisProgressStage.ParsingCoverage,
                $"Parsing {coberturaFiles.Length} Cobertura coverage file(s)...",
                percentComplete: 85,
                current: 0,
                total: coberturaFiles.Length,
                detail: coberturaFiles[0]);
            var result = _parser.Parse(coberturaFiles);
            Report(
                progress,
                CoverageAnalysisProgressStage.ParsingCoverage,
                "Finished parsing normalized coverage results.",
                percentComplete: 90,
                current: coberturaFiles.Length,
                total: coberturaFiles.Length,
                detail: $"{result.Projects.Count} project(s), {result.Classes.Count} class(es)");
            return result;
        }
        finally
        {
            TryDeleteDirectory(resultsDirectory);
        }
    }

    private static async Task<string> RunDotnetCoverageAsync(
        string solutionPath,
        string resultsDirectory,
        CancellationToken cancellationToken,
        IProgress<CoverageAnalysisProgressDto>? progress)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
            EnableRaisingEvents = true,
        };

        process.StartInfo.ArgumentList.Add("test");
        process.StartInfo.ArgumentList.Add(solutionPath);
        process.StartInfo.ArgumentList.Add("--collect:XPlat Code Coverage");
        process.StartInfo.ArgumentList.Add("--results-directory");
        process.StartInfo.ArgumentList.Add(resultsDirectory);
        process.StartInfo.ArgumentList.Add("--nologo");

        var commandPreview = BuildCommandPreview(process.StartInfo);
        Report(progress, CoverageAnalysisProgressStage.RunningTests, "Prepared dotnet test command.", percentComplete: 28, detail: commandPreview);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdout.AppendLine(args.Data);
                ReportDotNetOutput(progress, args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stderr.AppendLine(args.Data);
                Report(progress, CoverageAnalysisProgressStage.RunningTests, "dotnet test reported diagnostic output...", percentComplete: 45, detail: args.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start dotnet test for coverage analysis.");
        }

        Report(progress, CoverageAnalysisProgressStage.RunningTests, "dotnet test process started.", percentComplete: 30, detail: $"PID {process.Id}");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        Report(progress, CoverageAnalysisProgressStage.CollectingCoverage, $"dotnet test completed with exit code {process.ExitCode}; collecting coverage output...", percentComplete: 70);
        var combinedOutput = $"STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}";
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet test coverage execution failed with exit code {process.ExitCode}.{Environment.NewLine}{combinedOutput}");
        }

        return combinedOutput;
    }

    private static void ReportDotNetOutput(IProgress<CoverageAnalysisProgressDto>? progress, string outputLine)
    {
        var (stage, message, percentComplete) = ClassifyDotNetOutput(outputLine);
        Report(progress, stage, message, percentComplete: percentComplete, detail: outputLine);
    }

    private static (CoverageAnalysisProgressStage Stage, string Message, int PercentComplete) ClassifyDotNetOutput(string outputLine)
    {
        if (outputLine.Contains("Determining projects to restore", StringComparison.OrdinalIgnoreCase))
        {
            return (CoverageAnalysisProgressStage.RunningTests, "Restoring test project dependencies...", 34);
        }

        if (outputLine.Contains("All projects are up-to-date for restore", StringComparison.OrdinalIgnoreCase)
            || outputLine.Contains("Restored ", StringComparison.OrdinalIgnoreCase))
        {
            return (CoverageAnalysisProgressStage.RunningTests, "Restore completed; building test projects...", 38);
        }

        if (outputLine.Contains("Starting test execution", StringComparison.OrdinalIgnoreCase))
        {
            return (CoverageAnalysisProgressStage.RunningTests, "Starting discovered test execution...", 45);
        }

        if (outputLine.Contains("Passed!", StringComparison.OrdinalIgnoreCase)
            || outputLine.Contains("Failed!", StringComparison.OrdinalIgnoreCase))
        {
            return (CoverageAnalysisProgressStage.RunningTests, "Test execution summary received.", 65);
        }

        if (outputLine.Contains("Attachments:", StringComparison.OrdinalIgnoreCase)
            || outputLine.Contains("coverage.cobertura.xml", StringComparison.OrdinalIgnoreCase))
        {
            return (CoverageAnalysisProgressStage.CollectingCoverage, "Coverage attachment output detected.", 68);
        }

        if (outputLine.Contains(" -> ", StringComparison.Ordinal) && outputLine.Contains(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return (CoverageAnalysisProgressStage.RunningTests, "Build output produced for a test project.", 42);
        }

        return (CoverageAnalysisProgressStage.RunningTests, "dotnet test is running...", 45);
    }

    private static string BuildCommandPreview(ProcessStartInfo startInfo)
    {
        return string.Join(' ', new[] { startInfo.FileName }.Concat(startInfo.ArgumentList.Select(QuoteArgument)));
    }

    private static string QuoteArgument(string argument)
    {
        return argument.Contains(' ', StringComparison.Ordinal) || argument.Contains('"', StringComparison.Ordinal)
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;
    }

    private static void Report(
        IProgress<CoverageAnalysisProgressDto>? progress,
        CoverageAnalysisProgressStage stage,
        string message,
        int? percentComplete = null,
        int? current = null,
        int? total = null,
        string? detail = null)
        => progress?.Report(new CoverageAnalysisProgressDto(
            stage,
            message,
            percentComplete,
            current,
            total,
            detail));

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
