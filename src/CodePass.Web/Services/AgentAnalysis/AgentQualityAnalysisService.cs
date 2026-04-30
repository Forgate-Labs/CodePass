using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.Dashboard;
using CodePass.Web.Services.RuleAnalysis;
using CodePass.Web.Services.Solutions;

namespace CodePass.Web.Services.AgentAnalysis;

public sealed class AgentQualityAnalysisService(
    IRegisteredSolutionService registeredSolutionService,
    IRuleAnalysisRunService ruleAnalysisRunService,
    ICoverageAnalysisRunService coverageAnalysisRunService,
    IQualityScoreService qualityScoreService) : IAgentQualityAnalysisService
{
    public async Task<AgentQualityAnalysisResponse> AnalyzeAsync(
        Guid registeredSolutionId,
        AgentQualityAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.RunRuleAnalysis && !request.RunCoverageAnalysis)
        {
            throw new InvalidOperationException("At least one analysis must be enabled.");
        }

        var registeredSolutionExists = (await registeredSolutionService.GetAllAsync(cancellationToken))
            .Any(solution => solution.Id == registeredSolutionId);

        if (!registeredSolutionExists)
        {
            throw new KeyNotFoundException($"Registered solution '{registeredSolutionId}' was not found.");
        }

        var startedAtUtc = DateTimeOffset.UtcNow;

        RuleAnalysisSummaryDto? ruleAnalysis = null;
        if (request.RunRuleAnalysis)
        {
            var ruleRun = await ruleAnalysisRunService.StartRunAsync(registeredSolutionId, cancellationToken);
            ruleAnalysis = ruleRun.ToSummary();
        }

        CoverageAnalysisSummaryDto? coverageAnalysis = null;
        if (request.RunCoverageAnalysis)
        {
            var coverageRun = await coverageAnalysisRunService.StartRunAsync(registeredSolutionId, cancellationToken);
            coverageAnalysis = coverageRun.ToSummary();
        }

        var qualityScore = await qualityScoreService.GetCurrentSnapshotAsync(registeredSolutionId, cancellationToken);

        return new AgentQualityAnalysisResponse(
            registeredSolutionId,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            ruleAnalysis,
            coverageAnalysis,
            qualityScore);
    }
}
