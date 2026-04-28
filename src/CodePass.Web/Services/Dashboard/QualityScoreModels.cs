namespace CodePass.Web.Services.Dashboard;

public enum QualityScoreStatus
{
    Fail = 0,
    Pass = 1
}

public enum QualityEvidenceStatus
{
    Missing = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

public sealed record QualityScoreSnapshotDto(
    Guid RegisteredSolutionId,
    double Score,
    QualityScoreStatus Status,
    QualityRuleContributionDto RuleContribution,
    QualityCoverageContributionDto CoverageContribution,
    IReadOnlyList<string> BlockingReasons);

public sealed record QualityRuleContributionDto(
    double MaxPoints,
    double EarnedPoints,
    QualityEvidenceStatus EvidenceStatus,
    int ErrorCount,
    int WarningCount,
    int InfoCount,
    int TotalViolations,
    string Summary,
    IReadOnlyList<string> BlockingReasons);

public sealed record QualityCoverageContributionDto(
    double MaxPoints,
    double EarnedPoints,
    QualityEvidenceStatus EvidenceStatus,
    double? LineCoveragePercent,
    int? CoveredLineCount,
    int? TotalLineCount,
    string Summary,
    IReadOnlyList<string> BlockingReasons);
