namespace CodePass.Web.Services.CoverageAnalysis;

public interface ICoverageAnalysisResultService
{
    Task<CoverageAnalysisRunDto> CreateRunningRunAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default);

    Task<CoverageAnalysisRunDto> MarkSucceededAsync(
        Guid runId,
        CoverageAnalysisResult result,
        CancellationToken cancellationToken = default);

    Task<CoverageAnalysisRunDto> MarkFailedAsync(
        Guid runId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<CoverageAnalysisRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<CoverageAnalysisRunDto?> GetLatestRunForSolutionAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default);
}
