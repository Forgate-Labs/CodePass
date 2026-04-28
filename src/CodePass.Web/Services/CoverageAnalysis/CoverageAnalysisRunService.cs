using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Solutions;

namespace CodePass.Web.Services.CoverageAnalysis;

public sealed class CoverageAnalysisRunService(
    IRegisteredSolutionService registeredSolutionService,
    ICoverageAnalyzer coverageAnalyzer,
    ICoverageAnalysisResultService resultService) : ICoverageAnalysisRunService
{
    public async Task<CoverageAnalysisRunDto> StartRunAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default,
        IProgress<CoverageAnalysisProgressDto>? progress = null)
    {
        Report(progress, CoverageAnalysisProgressStage.Preparing, "Preparing coverage analysis...", percentComplete: 5);

        var registeredSolution = (await registeredSolutionService.GetAllAsync(cancellationToken))
            .SingleOrDefault(solution => solution.Id == registeredSolutionId);

        if (registeredSolution is null)
        {
            Report(progress, CoverageAnalysisProgressStage.Failed, "Registered solution was not found.", percentComplete: 100);
            throw new InvalidOperationException($"Registered solution '{registeredSolutionId}' was not found.");
        }

        Report(progress, CoverageAnalysisProgressStage.CreatingRun, "Creating coverage-analysis run...", percentComplete: 10);
        var runningRun = await resultService.CreateRunningRunAsync(registeredSolution.Id, cancellationToken);

        if (registeredSolution.Status != RegisteredSolutionStatus.Valid)
        {
            var result = await resultService.MarkFailedAsync(
                runningRun.Id,
                $"Only valid registered solutions can be analyzed for coverage. Current status: {registeredSolution.Status}.",
                cancellationToken);
            Report(progress, CoverageAnalysisProgressStage.Failed, "Coverage analysis cannot run for the selected solution.", percentComplete: 100, detail: result.ErrorMessage);
            return result;
        }

        try
        {
            Report(progress, CoverageAnalysisProgressStage.RunningTests, "Running dotnet test with coverage collection...", percentComplete: 20, detail: registeredSolution.SolutionPath);
            var result = await coverageAnalyzer.AnalyzeAsync(registeredSolution.SolutionPath, cancellationToken, progress);

            Report(progress, CoverageAnalysisProgressStage.SavingResults, $"Saving coverage results for {result.Projects.Count} project(s) and {result.Classes.Count} class(es)...", percentComplete: 95);
            var run = await resultService.MarkSucceededAsync(runningRun.Id, result, cancellationToken);
            Report(progress, CoverageAnalysisProgressStage.Completed, "Coverage analysis completed.", percentComplete: 100, detail: $"{run.LineCoveragePercent:0.#}% line coverage.");
            return run;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var errorMessage = string.IsNullOrWhiteSpace(exception.Message)
                ? "Coverage analysis failed."
                : exception.Message;

            Report(progress, CoverageAnalysisProgressStage.Failed, "Coverage analysis failed.", percentComplete: 100, detail: errorMessage);
            return await resultService.MarkFailedAsync(runningRun.Id, errorMessage, CancellationToken.None);
        }
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
}
