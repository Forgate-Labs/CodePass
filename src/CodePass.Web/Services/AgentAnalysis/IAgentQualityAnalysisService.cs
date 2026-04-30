namespace CodePass.Web.Services.AgentAnalysis;

public interface IAgentQualityAnalysisService
{
    Task<AgentQualityAnalysisResponse> AnalyzeAsync(
        Guid registeredSolutionId,
        AgentQualityAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
