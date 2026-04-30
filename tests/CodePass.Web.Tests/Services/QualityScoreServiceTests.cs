using CodePass.Web.Data.Entities;
using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.Dashboard;
using CodePass.Web.Services.RuleAnalysis;
using FluentAssertions;

namespace CodePass.Web.Tests.Services;

public sealed class QualityScoreServiceTests
{
    [Fact]
    public async Task GetCurrentSnapshotAsync_ShouldPassWithPerfectSucceededEvidence()
    {
        var registeredSolutionId = Guid.NewGuid();
        var service = CreateService(
            RuleRun(registeredSolutionId, RuleAnalysisRunStatus.Succeeded, []),
            CoverageRun(registeredSolutionId, CoverageAnalysisRunStatus.Succeeded, lineCoveragePercent: 100, coveredLines: 100, totalLines: 100));

        var snapshot = await service.GetCurrentSnapshotAsync(registeredSolutionId);

        snapshot.Score.Should().Be(100);
        snapshot.Status.Should().Be(QualityScoreStatus.Pass);
        snapshot.RuleContribution.EarnedPoints.Should().Be(100);
        snapshot.RuleContribution.MaxPoints.Should().Be(100);
        snapshot.RuleContribution.ErrorCount.Should().Be(0);
        snapshot.RuleContribution.WarningCount.Should().Be(0);
        snapshot.RuleContribution.InfoCount.Should().Be(0);
        snapshot.RuleContribution.TotalViolations.Should().Be(0);
        snapshot.CoverageContribution.EarnedPoints.Should().Be(100);
        snapshot.CoverageContribution.MaxPoints.Should().Be(100);
        snapshot.CoverageContribution.LineCoveragePercent.Should().Be(100);
        snapshot.BlockingReasons.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ShouldFailAndExposePartialContributionsWhenViolationsExist()
    {
        var registeredSolutionId = Guid.NewGuid();
        var service = CreateService(
            RuleRun(
                registeredSolutionId,
                RuleAnalysisRunStatus.Succeeded,
                [
                    RuleGroup(RuleSeverity.Error, violationCount: 1),
                    RuleGroup(RuleSeverity.Warning, violationCount: 1)
                ]),
            CoverageRun(registeredSolutionId, CoverageAnalysisRunStatus.Succeeded, lineCoveragePercent: 80, coveredLines: 80, totalLines: 100));

        var snapshot = await service.GetCurrentSnapshotAsync(registeredSolutionId);

        snapshot.Score.Should().Be(84.5);
        snapshot.Status.Should().Be(QualityScoreStatus.Fail);
        snapshot.RuleContribution.EarnedPoints.Should().Be(89);
        snapshot.RuleContribution.ErrorCount.Should().Be(1);
        snapshot.RuleContribution.WarningCount.Should().Be(1);
        snapshot.RuleContribution.InfoCount.Should().Be(0);
        snapshot.RuleContribution.TotalViolations.Should().Be(2);
        snapshot.CoverageContribution.EarnedPoints.Should().Be(80);
        snapshot.CoverageContribution.LineCoveragePercent.Should().Be(80);
        snapshot.BlockingReasons.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ShouldPenalizeManyErrorsEvenInLargeCodebases()
    {
        var registeredSolutionId = Guid.NewGuid();
        var service = CreateService(
            RuleRun(
                registeredSolutionId,
                RuleAnalysisRunStatus.Succeeded,
                [RuleGroup(RuleSeverity.Error, violationCount: 22)]),
            CoverageRun(registeredSolutionId, CoverageAnalysisRunStatus.Succeeded, lineCoveragePercent: 100, coveredLines: 10000, totalLines: 10000));

        var snapshot = await service.GetCurrentSnapshotAsync(registeredSolutionId);

        snapshot.Score.Should().Be(89.8);
        snapshot.Status.Should().Be(QualityScoreStatus.Fail);
        snapshot.RuleContribution.EarnedPoints.Should().Be(79.6);
        snapshot.RuleContribution.ErrorCount.Should().Be(22);
        snapshot.CoverageContribution.EarnedPoints.Should().Be(100);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ShouldUseSolutionPassThreshold()
    {
        var registeredSolutionId = Guid.NewGuid();
        var service = CreateService(
            RuleRun(registeredSolutionId, RuleAnalysisRunStatus.Succeeded, []),
            CoverageRun(registeredSolutionId, CoverageAnalysisRunStatus.Succeeded, lineCoveragePercent: 78, coveredLines: 78, totalLines: 100),
            passThreshold: 75);

        var snapshot = await service.GetCurrentSnapshotAsync(registeredSolutionId);

        snapshot.Score.Should().Be(89);
        snapshot.Status.Should().Be(QualityScoreStatus.Pass);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ShouldFailWithZeroScoreAndBlockingReasonsWhenEvidenceIsMissingOrFailed()
    {
        var registeredSolutionId = Guid.NewGuid();
        var service = CreateService(
            ruleRun: null,
            CoverageRun(
                registeredSolutionId,
                CoverageAnalysisRunStatus.Failed,
                lineCoveragePercent: 0,
                coveredLines: 0,
                totalLines: 0,
                errorMessage: "dotnet test exited with code 1."));

        var snapshot = await service.GetCurrentSnapshotAsync(registeredSolutionId);

        snapshot.Score.Should().Be(0);
        snapshot.Status.Should().Be(QualityScoreStatus.Fail);
        snapshot.RuleContribution.EarnedPoints.Should().Be(0);
        snapshot.RuleContribution.EvidenceStatus.Should().Be(QualityEvidenceStatus.Missing);
        snapshot.CoverageContribution.EarnedPoints.Should().Be(0);
        snapshot.CoverageContribution.EvidenceStatus.Should().Be(QualityEvidenceStatus.Failed);
        snapshot.BlockingReasons.Should().Contain(reason => reason.Contains("missing rule-analysis evidence", StringComparison.OrdinalIgnoreCase));
        snapshot.BlockingReasons.Should().Contain(reason => reason.Contains("dotnet test exited with code 1", StringComparison.OrdinalIgnoreCase));
    }

    private static IQualityScoreService CreateService(
        RuleAnalysisRunDto? ruleRun,
        CoverageAnalysisRunDto? coverageRun,
        double? passThreshold = null)
    {
        return new QualityScoreService(
            new FakeRuleAnalysisResultService(ruleRun),
            new FakeCoverageAnalysisResultService(coverageRun),
            passThreshold is null ? null : new FakeQualityScoreSettingsService(passThreshold.Value));
    }

    private static RuleAnalysisRunDto RuleRun(
        Guid registeredSolutionId,
        RuleAnalysisRunStatus status,
        IReadOnlyList<RuleAnalysisRuleGroupDto> ruleGroups,
        string? errorMessage = null)
    {
        return new RuleAnalysisRunDto(
            Guid.NewGuid(),
            registeredSolutionId,
            status,
            DateTimeOffset.UtcNow.AddMinutes(-2),
            status == RuleAnalysisRunStatus.Running ? null : DateTimeOffset.UtcNow.AddMinutes(-1),
            ruleGroups.Count,
            ruleGroups.Sum(group => group.ViolationCount),
            errorMessage,
            ruleGroups);
    }

    private static RuleAnalysisRuleGroupDto RuleGroup(RuleSeverity severity, int violationCount)
    {
        var ruleCode = $"CP{(int)severity}{violationCount:000}";

        return new RuleAnalysisRuleGroupDto(
            ruleCode,
            $"{severity} test rule",
            "test_rule",
            severity,
            violationCount,
            Enumerable.Range(1, violationCount)
                .Select(index => new RuleAnalysisViolationDto(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ruleCode,
                    $"{severity} test rule",
                    "test_rule",
                    severity,
                    $"{severity} violation {index}",
                    $"src/{severity}File{index}.cs",
                    index,
                    1,
                    index,
                    10))
                .ToList());
    }

    private static CoverageAnalysisRunDto CoverageRun(
        Guid registeredSolutionId,
        CoverageAnalysisRunStatus status,
        double lineCoveragePercent,
        int coveredLines,
        int totalLines,
        string? errorMessage = null)
    {
        return new CoverageAnalysisRunDto(
            Guid.NewGuid(),
            registeredSolutionId,
            status,
            DateTimeOffset.UtcNow.AddMinutes(-2),
            status == CoverageAnalysisRunStatus.Running ? null : DateTimeOffset.UtcNow.AddMinutes(-1),
            ProjectCount: totalLines > 0 ? 1 : 0,
            ClassCount: totalLines > 0 ? 1 : 0,
            CoveredLineCount: coveredLines,
            TotalLineCount: totalLines,
            LineCoveragePercent: lineCoveragePercent,
            CoveredBranchCount: 0,
            TotalBranchCount: 0,
            BranchCoveragePercent: 0,
            ErrorMessage: errorMessage,
            ProjectSummaries: [],
            ClassCoverages: []);
    }

    private sealed class FakeQualityScoreSettingsService(double passThreshold) : IQualityScoreSettingsService
    {
        public Task<QualityScoreSettingsDto> GetSettingsAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new QualityScoreSettingsDto(registeredSolutionId, passThreshold));

        public Task<QualityScoreSettingsDto> UpdateSettingsAsync(Guid registeredSolutionId, UpdateQualityScoreSettingsRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRuleAnalysisResultService(RuleAnalysisRunDto? latestRun) : IRuleAnalysisResultService
    {
        public Task<RuleAnalysisRunDto> CreateRunningRunAsync(Guid registeredSolutionId, int ruleCount, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuleAnalysisRunDto> MarkSucceededAsync(Guid runId, IReadOnlyList<RuleAnalysisFinding> findings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuleAnalysisRunDto> MarkFailedAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuleAnalysisRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuleAnalysisRunDto?> GetLatestRunForSolutionAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(latestRun?.RegisteredSolutionId == registeredSolutionId ? latestRun : null);
    }

    private sealed class FakeCoverageAnalysisResultService(CoverageAnalysisRunDto? latestRun) : ICoverageAnalysisResultService
    {
        public Task<CoverageAnalysisRunDto> CreateRunningRunAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CoverageAnalysisRunDto> MarkSucceededAsync(Guid runId, CoverageAnalysisResult result, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CoverageAnalysisRunDto> MarkFailedAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CoverageAnalysisRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CoverageAnalysisRunDto?> GetLatestRunForSolutionAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(latestRun?.RegisteredSolutionId == registeredSolutionId ? latestRun : null);
    }
}
