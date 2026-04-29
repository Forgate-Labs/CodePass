using CodePass.Web.Data.Entities;
using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.RuleAnalysis;

namespace CodePass.Web.Services.Dashboard;

public sealed class QualityScoreService(
    IRuleAnalysisResultService ruleAnalysisResultService,
    ICoverageAnalysisResultService coverageAnalysisResultService) : IQualityScoreService
{
    private const double RuleMaxPoints = 100;
    private const double CoverageMaxPoints = 100;

    public async Task<QualityScoreSnapshotDto> GetCurrentSnapshotAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default)
    {
        var ruleRun = await ruleAnalysisResultService.GetLatestRunForSolutionAsync(registeredSolutionId, cancellationToken);
        var coverageRun = await coverageAnalysisResultService.GetLatestRunForSolutionAsync(registeredSolutionId, cancellationToken);

        var ruleContribution = BuildRuleContribution(ruleRun, coverageRun?.TotalLineCount);
        var coverageContribution = BuildCoverageContribution(coverageRun);
        var score = RoundPoints((ruleContribution.EarnedPoints + coverageContribution.EarnedPoints) / 2);
        var blockingReasons = ruleContribution.BlockingReasons.Concat(coverageContribution.BlockingReasons).ToList();
        var status = IsPassingSnapshot(score, ruleRun, coverageRun, ruleContribution)
            ? QualityScoreStatus.Pass
            : QualityScoreStatus.Fail;

        return new QualityScoreSnapshotDto(
            registeredSolutionId,
            score,
            status,
            ruleContribution,
            coverageContribution,
            blockingReasons);
    }

    private static QualityRuleContributionDto BuildRuleContribution(RuleAnalysisRunDto? run, int? totalLineCount)
    {
        if (run is null)
        {
            const string reason = "Missing rule-analysis evidence for the selected solution.";

            return new QualityRuleContributionDto(
                RuleMaxPoints,
                EarnedPoints: 0,
                QualityEvidenceStatus.Missing,
                ErrorCount: 0,
                WarningCount: 0,
                InfoCount: 0,
                TotalViolations: 0,
                Summary: reason,
                BlockingReasons: [reason]);
        }

        var errorCount = CountViolations(run, RuleSeverity.Error);
        var warningCount = CountViolations(run, RuleSeverity.Warning);
        var infoCount = CountViolations(run, RuleSeverity.Info);
        var totalViolations = errorCount + warningCount + infoCount;
        var evidenceStatus = MapRuleStatus(run.Status);

        if (run.Status != RuleAnalysisRunStatus.Succeeded)
        {
            var reason = run.Status switch
            {
                RuleAnalysisRunStatus.Running => "Rule-analysis evidence is still running for the selected solution.",
                RuleAnalysisRunStatus.Failed => string.IsNullOrWhiteSpace(run.ErrorMessage)
                    ? "Rule-analysis evidence failed for the selected solution."
                    : $"Rule-analysis evidence failed: {run.ErrorMessage}",
                _ => "Rule-analysis evidence is not available for scoring."
            };

            return new QualityRuleContributionDto(
                RuleMaxPoints,
                EarnedPoints: 0,
                evidenceStatus,
                errorCount,
                warningCount,
                infoCount,
                totalViolations,
                Summary: reason,
                BlockingReasons: [reason]);
        }

        var scoringLineCount = totalLineCount.GetValueOrDefault();
        var incorrectLineCount = CountViolationLines(run);
        var warningLineCount = CountWarningOnlyViolationLines(run);
        var earnedPoints = CalculateRuleScore(
            scoringLineCount,
            incorrectLineCount,
            warningLineCount,
            totalViolations);
        var summary = totalViolations == 0
            ? "Rule-analysis evidence succeeded with no violations."
            : scoringLineCount > 0
                ? $"Rule-analysis evidence succeeded with {errorCount} errors, {warningCount} warnings, and {infoCount} info findings across {incorrectLineCount} affected line(s) out of {scoringLineCount} total line(s)."
                : $"Rule-analysis evidence succeeded with {errorCount} errors, {warningCount} warnings, and {infoCount} info findings.";

        return new QualityRuleContributionDto(
            RuleMaxPoints,
            RoundPoints(earnedPoints),
            evidenceStatus,
            errorCount,
            warningCount,
            infoCount,
            totalViolations,
            summary,
            BlockingReasons: []);
    }

    private static QualityCoverageContributionDto BuildCoverageContribution(CoverageAnalysisRunDto? run)
    {
        if (run is null)
        {
            const string reason = "Missing coverage-analysis evidence for the selected solution.";

            return new QualityCoverageContributionDto(
                CoverageMaxPoints,
                EarnedPoints: 0,
                QualityEvidenceStatus.Missing,
                LineCoveragePercent: null,
                CoveredLineCount: null,
                TotalLineCount: null,
                Summary: reason,
                BlockingReasons: [reason]);
        }

        var evidenceStatus = MapCoverageStatus(run.Status);

        if (run.Status != CoverageAnalysisRunStatus.Succeeded)
        {
            var reason = run.Status switch
            {
                CoverageAnalysisRunStatus.Running => "Coverage-analysis evidence is still running for the selected solution.",
                CoverageAnalysisRunStatus.Failed => string.IsNullOrWhiteSpace(run.ErrorMessage)
                    ? "Coverage-analysis evidence failed for the selected solution."
                    : $"Coverage-analysis evidence failed: {run.ErrorMessage}",
                _ => "Coverage-analysis evidence is not available for scoring."
            };

            return new QualityCoverageContributionDto(
                CoverageMaxPoints,
                EarnedPoints: 0,
                evidenceStatus,
                run.LineCoveragePercent,
                run.CoveredLineCount,
                run.TotalLineCount,
                Summary: reason,
                BlockingReasons: [reason]);
        }

        var earnedPoints = RoundPoints(Clamp(run.LineCoveragePercent, min: 0, max: CoverageMaxPoints));
        var summary = $"Coverage-analysis evidence succeeded with {run.LineCoveragePercent:0.#}% line coverage.";

        return new QualityCoverageContributionDto(
            CoverageMaxPoints,
            earnedPoints,
            evidenceStatus,
            run.LineCoveragePercent,
            run.CoveredLineCount,
            run.TotalLineCount,
            summary,
            BlockingReasons: []);
    }

    private static bool IsPassingSnapshot(
        double score,
        RuleAnalysisRunDto? ruleRun,
        CoverageAnalysisRunDto? coverageRun,
        QualityRuleContributionDto ruleContribution)
    {
        return score >= 80
            && ruleRun?.Status == RuleAnalysisRunStatus.Succeeded
            && coverageRun?.Status == CoverageAnalysisRunStatus.Succeeded
            && ruleContribution.ErrorCount == 0;
    }

    private static int CountViolations(RuleAnalysisRunDto run, RuleSeverity severity)
    {
        return run.RuleGroups
            .Where(group => group.Severity == severity)
            .Sum(group => group.ViolationCount);
    }

    private static int CountViolationLines(RuleAnalysisRunDto run)
    {
        return run.RuleGroups
            .SelectMany(group => group.Violations)
            .Select(violation => new { violation.FilePath, violation.StartLine })
            .Distinct()
            .Count();
    }

    private static int CountWarningOnlyViolationLines(RuleAnalysisRunDto run)
    {
        return run.RuleGroups
            .SelectMany(group => group.Violations.Select(violation => new
            {
                violation.FilePath,
                violation.StartLine,
                group.Severity
            }))
            .GroupBy(violation => new { violation.FilePath, violation.StartLine })
            .Count(group => group.All(violation => violation.Severity == RuleSeverity.Warning));
    }

    private static double CalculateRuleScore(
        int totalLineCount,
        int incorrectLineCount,
        int warningLineCount,
        int totalViolations)
    {
        if (totalLineCount <= 0)
        {
            return totalViolations == 0 ? RuleMaxPoints : 0;
        }

        var normalizedIncorrectLineCount = Math.Min(incorrectLineCount, totalLineCount);
        var normalizedWarningLineCount = Math.Min(warningLineCount, normalizedIncorrectLineCount);
        var correctLineCount = totalLineCount - normalizedIncorrectLineCount;
        var penalizedIncorrectLineCount = normalizedIncorrectLineCount - normalizedWarningLineCount;
        var score = (correctLineCount - penalizedIncorrectLineCount) / (double)totalLineCount * RuleMaxPoints;

        return RoundPoints(Clamp(score, min: 0, max: RuleMaxPoints));
    }

    private static QualityEvidenceStatus MapRuleStatus(RuleAnalysisRunStatus status)
    {
        return status switch
        {
            RuleAnalysisRunStatus.Running => QualityEvidenceStatus.Running,
            RuleAnalysisRunStatus.Succeeded => QualityEvidenceStatus.Succeeded,
            RuleAnalysisRunStatus.Failed => QualityEvidenceStatus.Failed,
            _ => QualityEvidenceStatus.Missing
        };
    }

    private static QualityEvidenceStatus MapCoverageStatus(CoverageAnalysisRunStatus status)
    {
        return status switch
        {
            CoverageAnalysisRunStatus.Running => QualityEvidenceStatus.Running,
            CoverageAnalysisRunStatus.Succeeded => QualityEvidenceStatus.Succeeded,
            CoverageAnalysisRunStatus.Failed => QualityEvidenceStatus.Failed,
            _ => QualityEvidenceStatus.Missing
        };
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static double RoundPoints(double value)
    {
        return Math.Round(value, 1, MidpointRounding.AwayFromZero);
    }
}
