using CodePass.Web.Data.Entities;
using CodePass.Web.Services.AgentAnalysis;
using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.Dashboard;
using CodePass.Web.Services.RuleAnalysis;
using CodePass.Web.Services.Solutions;
using FluentAssertions;

namespace CodePass.Web.Tests.Services;

public sealed class AgentQualityAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_RunsEnabledAnalysesAndReturnsQualitySummary()
    {
        var solutionId = Guid.NewGuid();
        var registeredSolutions = new FakeRegisteredSolutionService(solutionId);
        var ruleRuns = new FakeRuleAnalysisRunService();
        var coverageRuns = new FakeCoverageAnalysisRunService();
        var qualityScores = new FakeQualityScoreService(solutionId);
        var service = new AgentQualityAnalysisService(registeredSolutions, ruleRuns, coverageRuns, qualityScores);

        var response = await service.AnalyzeAsync(solutionId, new AgentQualityAnalysisRequest());

        response.RegisteredSolutionId.Should().Be(solutionId);
        response.RuleAnalysis.Should().NotBeNull();
        response.RuleAnalysis!.TotalViolations.Should().Be(2);
        response.RuleAnalysis.ErrorCount.Should().Be(1);
        response.RuleAnalysis.WarningCount.Should().Be(1);
        response.CoverageAnalysis.Should().NotBeNull();
        response.CoverageAnalysis!.LineCoveragePercent.Should().Be(87.5);
        response.CoverageAnalysis.BranchCoveragePercent.Should().Be(75);
        response.QualityScore.Score.Should().Be(82);
        ruleRuns.CallCount.Should().Be(1);
        coverageRuns.CallCount.Should().Be(1);
        qualityScores.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_CanSkipCoverageAnalysis()
    {
        var solutionId = Guid.NewGuid();
        var coverageRuns = new FakeCoverageAnalysisRunService();
        var service = new AgentQualityAnalysisService(
            new FakeRegisteredSolutionService(solutionId),
            new FakeRuleAnalysisRunService(),
            coverageRuns,
            new FakeQualityScoreService(solutionId));

        var response = await service.AnalyzeAsync(solutionId, new AgentQualityAnalysisRequest(RunCoverageAnalysis: false));

        response.RuleAnalysis.Should().NotBeNull();
        response.CoverageAnalysis.Should().BeNull();
        coverageRuns.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeAsync_ThrowsWhenSolutionDoesNotExist()
    {
        var service = new AgentQualityAnalysisService(
            new FakeRegisteredSolutionService(Guid.NewGuid()),
            new FakeRuleAnalysisRunService(),
            new FakeCoverageAnalysisRunService(),
            new FakeQualityScoreService(Guid.NewGuid()));

        var act = () => service.AnalyzeAsync(Guid.NewGuid(), new AgentQualityAnalysisRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private sealed class FakeRegisteredSolutionService(Guid solutionId) : IRegisteredSolutionService
    {
        public Task<IReadOnlyList<RegisteredSolution>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RegisteredSolution>>([
                new RegisteredSolution
                {
                    Id = solutionId,
                    DisplayName = "Test solution",
                    SolutionPath = "/tmp/Test.sln",
                    Status = RegisteredSolutionStatus.Valid
                }
            ]);

        public Task<RegisteredSolution> CreateAsync(CreateRegisteredSolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RegisteredSolution> UpdateAsync(Guid id, UpdateRegisteredSolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RegisteredSolution?> RefreshAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RefreshAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRuleAnalysisRunService : IRuleAnalysisRunService
    {
        public int CallCount { get; private set; }

        public Task<RuleAnalysisRunDto> StartRunAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default, IProgress<RuleAnalysisProgressDto>? progress = null)
        {
            CallCount++;
            return Task.FromResult(new RuleAnalysisRunDto(
                Guid.NewGuid(),
                registeredSolutionId,
                RuleAnalysisRunStatus.Succeeded,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                2,
                2,
                null,
                [
                    new RuleAnalysisRuleGroupDto("CP001", "Error rule", "Code", RuleSeverity.Error, 1, []),
                    new RuleAnalysisRuleGroupDto("CP002", "Warning rule", "Code", RuleSeverity.Warning, 1, [])
                ]));
        }
    }

    private sealed class FakeCoverageAnalysisRunService : ICoverageAnalysisRunService
    {
        public int CallCount { get; private set; }

        public Task<CoverageAnalysisRunDto> StartRunAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default, IProgress<CoverageAnalysisProgressDto>? progress = null)
        {
            CallCount++;
            return Task.FromResult(new CoverageAnalysisRunDto(
                Guid.NewGuid(),
                registeredSolutionId,
                CoverageAnalysisRunStatus.Succeeded,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                1,
                3,
                70,
                80,
                87.5,
                15,
                20,
                75,
                null,
                [],
                []));
        }
    }

    private sealed class FakeQualityScoreService(Guid solutionId) : IQualityScoreService
    {
        public int CallCount { get; private set; }

        public Task<QualityScoreSnapshotDto> GetCurrentSnapshotAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            registeredSolutionId.Should().Be(solutionId);

            return Task.FromResult(new QualityScoreSnapshotDto(
                registeredSolutionId,
                82,
                QualityScoreStatus.Pass,
                new QualityRuleContributionDto(50, 38, QualityEvidenceStatus.Succeeded, 1, 1, 0, 2, "2 violations", ["Rule blocker"]),
                new QualityCoverageContributionDto(50, 44, QualityEvidenceStatus.Succeeded, 87.5, 70, 80, "87.5% coverage", []),
                ["Project blocker"]));
        }
    }
}
