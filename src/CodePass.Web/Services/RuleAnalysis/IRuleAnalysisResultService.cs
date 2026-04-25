namespace CodePass.Web.Services.RuleAnalysis;

public interface IRuleAnalysisResultService
{
    Task<RuleAnalysisRunDto> CreateRunningRunAsync(
        Guid registeredSolutionId,
        int ruleCount,
        CancellationToken cancellationToken = default);

    Task<RuleAnalysisRunDto> MarkSucceededAsync(
        Guid runId,
        IReadOnlyList<RuleAnalysisFinding> findings,
        CancellationToken cancellationToken = default);

    Task<RuleAnalysisRunDto> MarkFailedAsync(
        Guid runId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<RuleAnalysisRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<RuleAnalysisRunDto?> GetLatestRunForSolutionAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default);
}
