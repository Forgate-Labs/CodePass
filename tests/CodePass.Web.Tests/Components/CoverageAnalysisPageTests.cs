using Bunit;
using CodePass.Web.Components.Pages;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class CoverageAnalysisPageTests : TestContext
{
    [Fact]
    public void EmptyState_ShouldRenderWhenNoRegisteredSolutionsExist()
    {
        Services.AddSingleton<IRegisteredSolutionService>(new CoverageAnalysisTestRegisteredSolutionService());
        Services.AddSingleton<ICoverageAnalysisRunService>(new CoverageAnalysisTestRunService());
        Services.AddSingleton<ICoverageAnalysisResultService>(new CoverageAnalysisTestResultService());

        var cut = RenderComponent<CoverageAnalysis>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='coverage-analysis-empty-state']").TextContent.Should().Contain("No registered solutions yet");
        });
    }

    [Fact]
    public void DefaultSelection_ShouldUseFirstValidRegisteredSolution()
    {
        var invalid = CoverageAnalysisComponentTestData.CreateSolution("Broken", "/solutions/broken.sln", RegisteredSolutionStatus.FileNotFound);
        var valid = CoverageAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var resultService = new CoverageAnalysisTestResultService();
        resultService.LatestRunsBySolution[valid.Id] = CoverageAnalysisComponentTestData.CreateSucceededRun(valid.Id, projectName: "Alpha.Project");

        Services.AddSingleton<IRegisteredSolutionService>(new CoverageAnalysisTestRegisteredSolutionService(invalid, valid));
        Services.AddSingleton<ICoverageAnalysisRunService>(new CoverageAnalysisTestRunService());
        Services.AddSingleton<ICoverageAnalysisResultService>(resultService);

        var cut = RenderComponent<CoverageAnalysis>();

        cut.WaitForAssertion(() =>
        {
            resultService.LatestCalls.Should().Contain(valid.Id);
            resultService.LatestCalls.Should().NotContain(invalid.Id);
            cut.Markup.Should().Contain("Alpha.Project");
            cut.Find("[data-testid='coverage-run-status']").TextContent.Should().Contain("Succeeded");
        });
    }

    [Fact]
    public async Task SelectingSolution_ShouldLoadItsLatestCoverageRun()
    {
        var alpha = CoverageAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var beta = CoverageAnalysisComponentTestData.CreateSolution("Beta", "/solutions/beta.sln", RegisteredSolutionStatus.Valid);
        var resultService = new CoverageAnalysisTestResultService();
        resultService.LatestRunsBySolution[alpha.Id] = CoverageAnalysisComponentTestData.CreateSucceededRun(alpha.Id, projectName: "Alpha.Project");
        resultService.LatestRunsBySolution[beta.Id] = CoverageAnalysisComponentTestData.CreateSucceededRun(beta.Id, projectName: "Beta.Project");

        Services.AddSingleton<IRegisteredSolutionService>(new CoverageAnalysisTestRegisteredSolutionService(alpha, beta));
        Services.AddSingleton<ICoverageAnalysisRunService>(new CoverageAnalysisTestRunService());
        Services.AddSingleton<ICoverageAnalysisResultService>(resultService);

        var cut = RenderComponent<CoverageAnalysis>();

        cut.WaitForAssertion(() =>
        {
            resultService.LatestCalls.Should().Contain(alpha.Id);
            cut.Markup.Should().Contain("Alpha.Project");
        });

        await cut.FindAll("[data-testid='select-solution-button']")[1].ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            resultService.LatestCalls.Should().Contain(beta.Id);
            cut.Markup.Should().Contain("Beta.Project");
            cut.Markup.Should().NotContain("Alpha.Project");
        });
    }

    [Fact]
    public async Task RunCoverageAnalysis_ShouldCallStartRunForSelectedSolution()
    {
        var solution = CoverageAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var runService = new CoverageAnalysisTestRunService();
        var resultService = new CoverageAnalysisTestResultService();
        resultService.LatestRunsBySolution[solution.Id] = CoverageAnalysisComponentTestData.CreateSucceededRun(solution.Id);

        Services.AddSingleton<IRegisteredSolutionService>(new CoverageAnalysisTestRegisteredSolutionService(solution));
        Services.AddSingleton<ICoverageAnalysisRunService>(runService);
        Services.AddSingleton<ICoverageAnalysisResultService>(resultService);

        var cut = RenderComponent<CoverageAnalysis>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='run-coverage-analysis-button']").TextContent.Should().Contain("Run coverage analysis"));

        await cut.Find("[data-testid='run-coverage-analysis-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            runService.StartCalls.Should().ContainSingle(id => id == solution.Id);
        });
    }

    [Fact]
    public async Task RunCoverageAnalysis_ShouldRenderProgressWhileRunIsActive()
    {
        var solution = CoverageAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var runService = new CoverageAnalysisTestRunService
        {
            PendingRun = new TaskCompletionSource<CoverageAnalysisRunDto>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var resultService = new CoverageAnalysisTestResultService();

        Services.AddSingleton<IRegisteredSolutionService>(new CoverageAnalysisTestRegisteredSolutionService(solution));
        Services.AddSingleton<ICoverageAnalysisRunService>(runService);
        Services.AddSingleton<ICoverageAnalysisResultService>(resultService);

        var cut = RenderComponent<CoverageAnalysis>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='run-coverage-analysis-button']").TextContent.Should().Contain("Run coverage analysis"));

        var clickTask = cut.Find("[data-testid='run-coverage-analysis-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='coverage-analysis-progress']");
            cut.Find("[data-testid='coverage-analysis-progress-stage']").TextContent.Should().Contain("Running tests");
            cut.Find("[data-testid='coverage-analysis-progress-message']").TextContent.Should().Contain("dotnet test is running");
            cut.Find("[data-testid='coverage-analysis-progress-detail']").TextContent.Should().Contain("Passed!  - Failed: 0");
            cut.Find("[data-testid='coverage-analysis-progress-count']").TextContent.Should().Contain("1 / 3");
            cut.Find("[data-testid='coverage-analysis-progress-bar']").GetAttribute("aria-valuenow").Should().Be("45");
            cut.Find("[data-testid='coverage-analysis-progress-history']").TextContent.Should().Contain("Execution details");
            cut.FindAll("[data-testid='coverage-analysis-progress-history-item']").Should().HaveCountGreaterThanOrEqualTo(2);
            cut.Find("[data-testid='coverage-analysis-progress-history']").TextContent.Should().Contain("Passed!  - Failed: 0");
        });

        runService.PendingRun.SetResult(CoverageAnalysisComponentTestData.CreateSucceededRun(solution.Id));
        await clickTask;

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='run-coverage-analysis-button']").TextContent.Should().Contain("Run coverage analysis");
        });
    }

    [Fact]
    public async Task RunFailure_ShouldRenderReadableError()
    {
        var solution = CoverageAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var runService = new CoverageAnalysisTestRunService { ErrorToThrow = new InvalidOperationException("Coverage analyzer failed.") };

        Services.AddSingleton<IRegisteredSolutionService>(new CoverageAnalysisTestRegisteredSolutionService(solution));
        Services.AddSingleton<ICoverageAnalysisRunService>(runService);
        Services.AddSingleton<ICoverageAnalysisResultService>(new CoverageAnalysisTestResultService());

        var cut = RenderComponent<CoverageAnalysis>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='run-coverage-analysis-button']"));

        await cut.Find("[data-testid='run-coverage-analysis-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='coverage-analysis-run-error']").TextContent.Should().Contain("Coverage analyzer failed.");
        });
    }

    [Fact]
    public async Task SuccessfulRun_ShouldRefreshLatestCoverageResults()
    {
        var solution = CoverageAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var refreshedRun = CoverageAnalysisComponentTestData.CreateSucceededRun(solution.Id, projectName: "Refreshed.Project");
        var runService = new CoverageAnalysisTestRunService();
        var resultService = new CoverageAnalysisTestResultService();
        resultService.LatestRunsBySolution[solution.Id] = null;
        runService.OnStart = id => resultService.LatestRunsBySolution[id] = refreshedRun;

        Services.AddSingleton<IRegisteredSolutionService>(new CoverageAnalysisTestRegisteredSolutionService(solution));
        Services.AddSingleton<ICoverageAnalysisRunService>(runService);
        Services.AddSingleton<ICoverageAnalysisResultService>(resultService);

        var cut = RenderComponent<CoverageAnalysis>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='coverage-analysis-no-results']"));

        await cut.Find("[data-testid='run-coverage-analysis-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            runService.StartCalls.Should().ContainSingle(id => id == solution.Id);
            resultService.LatestCalls.Count(id => id == solution.Id).Should().BeGreaterThanOrEqualTo(2);
            cut.Markup.Should().Contain("Refreshed.Project");
        });
    }
}

internal sealed class CoverageAnalysisTestRegisteredSolutionService(params RegisteredSolution[] seededSolutions) : IRegisteredSolutionService
{
    private readonly List<RegisteredSolution> _solutions = seededSolutions.ToList();

    public Task<IReadOnlyList<RegisteredSolution>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RegisteredSolution>>(_solutions.ToList());

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

internal sealed class CoverageAnalysisTestRunService : ICoverageAnalysisRunService
{
    public List<Guid> StartCalls { get; } = [];
    public Exception? ErrorToThrow { get; init; }
    public Action<Guid>? OnStart { get; set; }
    public TaskCompletionSource<CoverageAnalysisRunDto>? PendingRun { get; set; }

    public Task<CoverageAnalysisRunDto> StartRunAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default,
        IProgress<CoverageAnalysisProgressDto>? progress = null)
    {
        StartCalls.Add(registeredSolutionId);
        progress?.Report(new CoverageAnalysisProgressDto(
            CoverageAnalysisProgressStage.RunningTests,
            "dotnet test is running...",
            PercentComplete: 45,
            Current: 1,
            Total: 3,
            Detail: "Passed!  - Failed: 0"));

        if (ErrorToThrow is not null)
        {
            throw ErrorToThrow;
        }

        if (PendingRun is not null)
        {
            return PendingRun.Task;
        }

        OnStart?.Invoke(registeredSolutionId);
        return Task.FromResult(CoverageAnalysisComponentTestData.CreateSucceededRun(registeredSolutionId));
    }
}

internal sealed class CoverageAnalysisTestResultService : ICoverageAnalysisResultService
{
    public Dictionary<Guid, CoverageAnalysisRunDto?> LatestRunsBySolution { get; } = [];
    public List<Guid> LatestCalls { get; } = [];

    public Task<CoverageAnalysisRunDto> CreateRunningRunAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<CoverageAnalysisRunDto> MarkSucceededAsync(Guid runId, CoverageAnalysisResult result, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<CoverageAnalysisRunDto> MarkFailedAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<CoverageAnalysisRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<CoverageAnalysisRunDto?> GetLatestRunForSolutionAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        LatestCalls.Add(registeredSolutionId);
        return Task.FromResult(LatestRunsBySolution.GetValueOrDefault(registeredSolutionId));
    }
}
