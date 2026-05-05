using Bunit;
using CodePass.Web.Components.Layout;
using CodePass.Web.Components.Pages;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Dashboard;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class DashboardPageTests : TestContext
{
    [Fact]
    public void EmptyState_ShouldRenderWhenNoRegisteredSolutionsExist()
    {
        Services.AddSingleton<IRegisteredSolutionService>(new DashboardTestRegisteredSolutionService());
        Services.AddSingleton<IQualityScoreService>(new DashboardTestQualityScoreService());

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='dashboard-empty-state']").TextContent.Should().Contain("No registered solutions yet");
            cut.FindAll("[data-testid='dashboard-solution-list']").Should().BeEmpty();
        });
    }

    [Fact]
    public void SolutionLoadFailure_ShouldRenderReadableLoadError()
    {
        Services.AddSingleton<IRegisteredSolutionService>(new DashboardTestRegisteredSolutionService
        {
            ErrorToThrow = new InvalidOperationException("Local database unavailable.")
        });
        Services.AddSingleton<IQualityScoreService>(new DashboardTestQualityScoreService());

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='dashboard-load-error']").TextContent.Should().Contain("Local database unavailable.");
            cut.Find("[data-testid='dashboard-empty-state']").TextContent.Should().Contain("No registered solutions yet");
        });
    }

    [Fact]
    public void DefaultSelection_ShouldUseFirstValidRegisteredSolutionAndRenderScoreComponents()
    {
        var invalid = DashboardComponentTestData.CreateSolution("Broken", "/solutions/broken.sln", RegisteredSolutionStatus.FileNotFound);
        var valid = DashboardComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var scoreService = new DashboardTestQualityScoreService();
        scoreService.SnapshotsBySolution[valid.Id] = DashboardComponentTestData.CreateSnapshot(valid.Id, QualityScoreStatus.Pass, score: 91.5);

        Services.AddSingleton<IRegisteredSolutionService>(new DashboardTestRegisteredSolutionService(invalid, valid));
        Services.AddSingleton<IQualityScoreService>(scoreService);

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            scoreService.ScoreCalls.Should().Contain(valid.Id);
            scoreService.ScoreCalls.Should().NotContain(invalid.Id);
            cut.Find("[data-testid='quality-score-summary']").TextContent.Should().Contain("Alpha");
            cut.Find("[data-testid='quality-score-value']").TextContent.Should().Be("91.5");
            cut.Find("[data-testid='quality-status-badge']").TextContent.Should().Be("Pass");
            cut.Find("[data-testid='quality-evidence-breakdown']").TextContent.Should().Contain("Rule analysis contribution");
            cut.Find("[data-testid='coverage-contribution-card']").TextContent.Should().Contain("Coverage contribution");
        });
    }

    [Fact]
    public async Task SelectedSolution_ShouldBePreservedInAnalysisLinks()
    {
        var solution = DashboardComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var scoreService = new DashboardTestQualityScoreService();
        scoreService.SnapshotsBySolution[solution.Id] = DashboardComponentTestData.CreateSnapshot(solution.Id, QualityScoreStatus.Pass, score: 95);

        Services.AddSingleton<IRegisteredSolutionService>(new DashboardTestRegisteredSolutionService(solution));
        Services.AddSingleton<IQualityScoreService>(scoreService);

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rule-contribution-card'] a")
                .GetAttribute("href")
                .Should().Be($"/solutions/{solution.Id}/analysis/rules");
            cut.Find("[data-testid='coverage-contribution-card'] a")
                .GetAttribute("href")
                .Should().Be($"/solutions/{solution.Id}/analysis/coverage");
        });
    }

    [Fact]
    public async Task SelectingSolution_ShouldLoadCurrentSnapshotForSelectedSolution()
    {
        var alpha = DashboardComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var beta = DashboardComponentTestData.CreateSolution("Beta", "/solutions/beta.sln", RegisteredSolutionStatus.Valid);
        var scoreService = new DashboardTestQualityScoreService();
        scoreService.SnapshotsBySolution[alpha.Id] = DashboardComponentTestData.CreateSnapshot(alpha.Id, QualityScoreStatus.Pass, score: 95);
        scoreService.SnapshotsBySolution[beta.Id] = DashboardComponentTestData.CreateSnapshot(beta.Id, QualityScoreStatus.Fail, score: 62);

        Services.AddSingleton<IRegisteredSolutionService>(new DashboardTestRegisteredSolutionService(alpha, beta));
        Services.AddSingleton<IQualityScoreService>(scoreService);

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            scoreService.ScoreCalls.Should().Contain(alpha.Id);
            cut.Find("[data-testid='quality-score-summary']").TextContent.Should().Contain("Alpha");
            cut.Find("[data-testid='quality-score-value']").TextContent.Should().Be("95");
        });

        await cut.FindAll("[data-testid='select-dashboard-solution-button']")[1].ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            scoreService.ScoreCalls.Should().Contain(beta.Id);
            cut.Find("[data-testid='quality-score-summary']").TextContent.Should().Contain("Beta");
            cut.Find("[data-testid='quality-score-value']").TextContent.Should().Be("62");
            cut.Find("[data-testid='quality-status-badge']").TextContent.Should().Be("Fail");
        });
    }

    [Fact]
    public async Task NoValidSolution_ShouldAllowSelectingRegisteredSolutionToInspectEvidence()
    {
        var invalid = DashboardComponentTestData.CreateSolution("Broken", "/solutions/broken.sln", RegisteredSolutionStatus.FileNotFound, "Missing file.");
        var scoreService = new DashboardTestQualityScoreService();
        scoreService.SnapshotsBySolution[invalid.Id] = DashboardComponentTestData.CreateSnapshot(invalid.Id, QualityScoreStatus.Fail, score: 40);

        Services.AddSingleton<IRegisteredSolutionService>(new DashboardTestRegisteredSolutionService(invalid));
        Services.AddSingleton<IQualityScoreService>(scoreService);

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='dashboard-no-valid-solution-message']").TextContent.Should().Contain("Select a registered solution");
            scoreService.ScoreCalls.Should().BeEmpty();
        });

        await cut.Find("[data-testid='select-dashboard-solution-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            scoreService.ScoreCalls.Should().ContainSingle(id => id == invalid.Id);
            cut.Find("[data-testid='dashboard-invalid-solution-message']").TextContent.Should().Contain("File not found");
            cut.Find("[data-testid='quality-score-value']").TextContent.Should().Be("40");
        });
    }

    [Fact]
    public void ScoreLoadFailure_ShouldRenderReadableErrorAndEmptyChildStates()
    {
        var solution = DashboardComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var scoreService = new DashboardTestQualityScoreService();
        scoreService.ErrorsBySolution[solution.Id] = new InvalidOperationException("Score evidence could not be read.");

        Services.AddSingleton<IRegisteredSolutionService>(new DashboardTestRegisteredSolutionService(solution));
        Services.AddSingleton<IQualityScoreService>(scoreService);

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='dashboard-score-load-error']").TextContent.Should().Contain("Score evidence could not be read.");
            cut.Find("[data-testid='quality-score-empty-state']").TextContent.Should().Contain("No current quality score");
            cut.Find("[data-testid='quality-evidence-empty-state']").TextContent.Should().Contain("Run rule analysis and coverage analysis");
        });
    }

    [Fact]
    public void NavMenu_ShouldExposeScoreWhenSolutionIsSelected()
    {
        var solutionId = Guid.NewGuid();
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo($"/solutions/{solutionId}/analysis/rules");

        var cut = RenderComponent<NavMenu>();

        var links = cut.FindAll("a.nav-link");
        links.Should().Contain(link => link.GetAttribute("href") == $"/solutions/{solutionId}/dashboard" && link.TextContent.Contains("Score"));
        links.Should().Contain(link => link.GetAttribute("href") == $"/solutions/{solutionId}/analysis/rules" && link.TextContent.Contains("Rule Analysis"));
        links.Should().Contain(link => link.GetAttribute("href") == $"/solutions/{solutionId}/analysis/coverage" && link.TextContent.Contains("Coverage Analysis"));

        links.Select(link => link.GetAttribute("href")).Should().StartWith($"/solutions/{solutionId}/dashboard");
    }
}

internal static class DashboardComponentTestData
{
    public static RegisteredSolution CreateSolution(string displayName, string path, RegisteredSolutionStatus status, string? statusMessage = null)
        => new()
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            SolutionPath = path,
            Status = status,
            StatusMessage = statusMessage,
            LastValidatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    public static QualityScoreSnapshotDto CreateSnapshot(Guid solutionId, QualityScoreStatus status, double score)
        => new(
            solutionId,
            score,
            status,
            new QualityRuleContributionDto(
                100,
                status == QualityScoreStatus.Pass ? 100 : 40,
                QualityEvidenceStatus.Succeeded,
                status == QualityScoreStatus.Pass ? 0 : 1,
                status == QualityScoreStatus.Pass ? 0 : 2,
                0,
                status == QualityScoreStatus.Pass ? 0 : 3,
                status == QualityScoreStatus.Pass ? "Rule analysis found no violations." : "Rule analysis found 3 violations.",
                status == QualityScoreStatus.Pass ? [] : ["Rule-analysis violations block a passing score."]),
            new QualityCoverageContributionDto(
                100,
                status == QualityScoreStatus.Pass ? 83 : 40,
                QualityEvidenceStatus.Succeeded,
                status == QualityScoreStatus.Pass ? 83 : 40,
                status == QualityScoreStatus.Pass ? 83 : 40,
                100,
                status == QualityScoreStatus.Pass ? "Line coverage is 83.0%." : "Line coverage is 40.0%.",
                []),
            status == QualityScoreStatus.Pass ? [] : ["Latest evidence does not pass."]);
}

internal sealed class DashboardTestRegisteredSolutionService(params RegisteredSolution[] seededSolutions) : IRegisteredSolutionService
{
    private readonly List<RegisteredSolution> _solutions = seededSolutions.ToList();

    public Exception? ErrorToThrow { get; init; }

    public Task<IReadOnlyList<RegisteredSolution>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (ErrorToThrow is not null)
        {
            throw ErrorToThrow;
        }

        return Task.FromResult<IReadOnlyList<RegisteredSolution>>(_solutions.ToList());
    }

    public Task<RegisteredSolution> CreateAsync(CreateRegisteredSolutionRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RegisteredSolution> UpdateAsync(Guid id, UpdateRegisteredSolutionRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RegisteredSolution?> RefreshAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_solutions.SingleOrDefault(solution => solution.Id == id));

    public Task<int> RefreshAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_solutions.Count);
}

internal sealed class DashboardTestQualityScoreService : IQualityScoreService
{
    public Dictionary<Guid, QualityScoreSnapshotDto> SnapshotsBySolution { get; } = [];
    public Dictionary<Guid, Exception> ErrorsBySolution { get; } = [];
    public List<Guid> ScoreCalls { get; } = [];

    public Task<QualityScoreSnapshotDto> GetCurrentSnapshotAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        ScoreCalls.Add(registeredSolutionId);

        if (ErrorsBySolution.TryGetValue(registeredSolutionId, out var exception))
        {
            throw exception;
        }

        return Task.FromResult(SnapshotsBySolution.GetValueOrDefault(
            registeredSolutionId,
            DashboardComponentTestData.CreateSnapshot(registeredSolutionId, QualityScoreStatus.Fail, score: 0)));
    }
}
