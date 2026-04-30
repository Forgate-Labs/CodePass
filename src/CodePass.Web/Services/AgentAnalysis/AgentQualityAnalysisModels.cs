using CodePass.Web.Data.Entities;
using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.Dashboard;
using CodePass.Web.Services.RuleAnalysis;

namespace CodePass.Web.Services.AgentAnalysis;

public sealed record AgentQualityAnalysisRequest(
    bool RunRuleAnalysis = true,
    bool RunCoverageAnalysis = true);

public sealed record AgentRegisteredSolutionDto(
    Guid Id,
    string DisplayName,
    string SolutionPath,
    string Status,
    string? StatusMessage,
    DateTimeOffset? LastValidatedAtUtc);

public sealed record AgentQualityAnalysisResponse(
    Guid RegisteredSolutionId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    RuleAnalysisSummaryDto? RuleAnalysis,
    CoverageAnalysisSummaryDto? CoverageAnalysis,
    QualityScoreSnapshotDto QualityScore);

public sealed record RuleAnalysisSummaryDto(
    Guid RunId,
    string Status,
    int RuleCount,
    int TotalViolations,
    int ErrorCount,
    int WarningCount,
    int InfoCount,
    string? ErrorMessage);

public sealed record CoverageAnalysisSummaryDto(
    Guid RunId,
    string Status,
    int ProjectCount,
    int ClassCount,
    int CoveredLineCount,
    int TotalLineCount,
    double LineCoveragePercent,
    int CoveredBranchCount,
    int TotalBranchCount,
    double BranchCoveragePercent,
    string? ErrorMessage);

internal static class AgentQualityAnalysisModelFactory
{
    public static AgentRegisteredSolutionDto ToAgentDto(this RegisteredSolution solution)
        => new(
            solution.Id,
            solution.DisplayName,
            solution.SolutionPath,
            solution.Status.ToString(),
            solution.StatusMessage,
            solution.LastValidatedAtUtc);


    public static RuleAnalysisSummaryDto ToSummary(this RuleAnalysisRunDto run)
        => new(
            run.Id,
            run.Status.ToString(),
            run.RuleCount,
            run.TotalViolations,
            run.RuleGroups.Where(group => group.Severity == Data.Entities.RuleSeverity.Error).Sum(group => group.ViolationCount),
            run.RuleGroups.Where(group => group.Severity == Data.Entities.RuleSeverity.Warning).Sum(group => group.ViolationCount),
            run.RuleGroups.Where(group => group.Severity == Data.Entities.RuleSeverity.Info).Sum(group => group.ViolationCount),
            run.ErrorMessage);

    public static CoverageAnalysisSummaryDto ToSummary(this CoverageAnalysisRunDto run)
        => new(
            run.Id,
            run.Status.ToString(),
            run.ProjectCount,
            run.ClassCount,
            run.CoveredLineCount,
            run.TotalLineCount,
            run.LineCoveragePercent,
            run.CoveredBranchCount,
            run.TotalBranchCount,
            run.BranchCoveragePercent,
            run.ErrorMessage);
}
