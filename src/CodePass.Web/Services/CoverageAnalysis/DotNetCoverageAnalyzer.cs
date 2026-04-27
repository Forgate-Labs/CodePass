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

    public async Task<CoverageAnalysisResult> AnalyzeAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        var resultsDirectory = Path.Combine(Path.GetTempPath(), "codepass-coverage-analysis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(resultsDirectory);

        try
        {
            var output = await RunDotnetCoverageAsync(solutionPath, resultsDirectory, cancellationToken);
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

            return _parser.Parse(coberturaFiles);
        }
        finally
        {
            TryDeleteDirectory(resultsDirectory);
        }
    }

    private static async Task<string> RunDotnetCoverageAsync(
        string solutionPath,
        string resultsDirectory,
        CancellationToken cancellationToken)
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

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdout.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stderr.AppendLine(args.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start dotnet test for coverage analysis.");
        }

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

        var combinedOutput = $"STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}";
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet test coverage execution failed with exit code {process.ExitCode}.{Environment.NewLine}{combinedOutput}");
        }

        return combinedOutput;
    }

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
