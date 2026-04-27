using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Solutions;

namespace CodePass.Web.Services.CoverageAnalysis;

public sealed class CoverageAnalysisRunService(
    IRegisteredSolutionService registeredSolutionService,
    ICoverageAnalyzer coverageAnalyzer,
    ICoverageAnalysisResultService resultService) : ICoverageAnalysisRunService
{
    public async Task<CoverageAnalysisRunDto> StartRunAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        var registeredSolution = (await registeredSolutionService.GetAllAsync(cancellationToken))
            .SingleOrDefault(solution => solution.Id == registeredSolutionId);

        if (registeredSolution is null)
        {
            throw new InvalidOperationException($"Registered solution '{registeredSolutionId}' was not found.");
        }

        var runningRun = await resultService.CreateRunningRunAsync(registeredSolution.Id, cancellationToken);

        if (registeredSolution.Status != RegisteredSolutionStatus.Valid)
        {
            return await resultService.MarkFailedAsync(
                runningRun.Id,
                $"Only valid registered solutions can be analyzed for coverage. Current status: {registeredSolution.Status}.",
                cancellationToken);
        }

        try
        {
            var result = await coverageAnalyzer.AnalyzeAsync(registeredSolution.SolutionPath, cancellationToken);

            return await resultService.MarkSucceededAsync(runningRun.Id, result, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var errorMessage = string.IsNullOrWhiteSpace(exception.Message)
                ? "Coverage analysis failed."
                : exception.Message;

            return await resultService.MarkFailedAsync(runningRun.Id, errorMessage, CancellationToken.None);
        }
    }
}
