using Bunit;
using CodePass.Web.Components.Pages;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.RuleAnalysis;
using CodePass.Web.Services.Rules;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class RuleAnalysisPageTests : TestContext
{
    [Fact]
    public void EmptyState_ShouldRenderWhenNoRegisteredSolutionsExist()
    {
        Services.AddSingleton<IRegisteredSolutionService>(new RuleAnalysisTestRegisteredSolutionService());
        Services.AddSingleton<ISolutionRuleSelectionService>(new RuleAnalysisTestSelectionService());
        Services.AddSingleton<IRuleAnalysisRunService>(new RuleAnalysisTestRunService());
        Services.AddSingleton<IRuleAnalysisResultService>(new RuleAnalysisTestResultService());

        var cut = RenderComponent<RuleAnalysis>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rule-analysis-empty-state']").TextContent.Should().Contain("No registered solutions yet");
        });
    }

    [Fact]
    public void SelectedSolution_ShouldRenderDisplayNameInAuthoredRulesHeading()
    {
        var solution = RuleAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);

        Services.AddSingleton<IRegisteredSolutionService>(new RuleAnalysisTestRegisteredSolutionService(solution));
        Services.AddSingleton<ISolutionRuleSelectionService>(new RuleAnalysisTestSelectionService());
        Services.AddSingleton<IRuleAnalysisRunService>(new RuleAnalysisTestRunService());
        Services.AddSingleton<IRuleAnalysisResultService>(new RuleAnalysisTestResultService());

        var cut = RenderComponent<RuleAnalysis>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Authored rules for Alpha");
            cut.Markup.Should().NotContain("Authored rules for SelectedSolution.DisplayName");
        });
    }

    [Fact]
    public async Task SelectingSolution_ShouldLoadPerSolutionAuthoredRuleSelections()
    {
        var alpha = RuleAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var beta = RuleAnalysisComponentTestData.CreateSolution("Beta", "/solutions/beta.sln", RegisteredSolutionStatus.Valid);
        var selectionService = new RuleAnalysisTestSelectionService();
        selectionService.SelectionsBySolution[alpha.Id] = [RuleAnalysisComponentTestData.CreateSelection("CP1000", "Alpha rule")];
        selectionService.SelectionsBySolution[beta.Id] = [RuleAnalysisComponentTestData.CreateSelection("CP2000", "Beta rule")];

        Services.AddSingleton<IRegisteredSolutionService>(new RuleAnalysisTestRegisteredSolutionService(alpha, beta));
        Services.AddSingleton<ISolutionRuleSelectionService>(selectionService);
        Services.AddSingleton<IRuleAnalysisRunService>(new RuleAnalysisTestRunService());
        Services.AddSingleton<IRuleAnalysisResultService>(new RuleAnalysisTestResultService());

        var cut = RenderComponent<RuleAnalysis>();

        cut.WaitForAssertion(() =>
        {
            selectionService.GetSelectionCalls.Should().Contain(alpha.Id);
            cut.Markup.Should().Contain("Alpha rule");
        });

        await cut.FindAll("[data-testid='select-solution-button']")[1].ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            selectionService.GetSelectionCalls.Should().Contain(beta.Id);
            cut.Markup.Should().Contain("Beta rule");
            cut.Markup.Should().NotContain("Alpha rule");
        });
    }

    [Fact]
    public async Task RunRuleAnalysis_ShouldCallStartRunAndRenderReturnedGroupedResults()
    {
        var solution = RuleAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var run = RuleAnalysisComponentTestData.CreateSucceededRun(solution.Id, totalViolations: 1);
        var runService = new RuleAnalysisTestRunService();
        runService.RunsBySolution[solution.Id] = run;

        Services.AddSingleton<IRegisteredSolutionService>(new RuleAnalysisTestRegisteredSolutionService(solution));
        Services.AddSingleton<ISolutionRuleSelectionService>(new RuleAnalysisTestSelectionService());
        Services.AddSingleton<IRuleAnalysisRunService>(runService);
        Services.AddSingleton<IRuleAnalysisResultService>(new RuleAnalysisTestResultService());

        var cut = RenderComponent<RuleAnalysis>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='run-rule-analysis-button']").TextContent.Should().Contain("Run rule analysis"));

        await cut.Find("[data-testid='run-rule-analysis-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            runService.StartCalls.Should().ContainSingle(id => id == solution.Id);
            cut.Markup.Should().Contain("Avoid Console.WriteLine");
            cut.Markup.Should().Contain("Program.cs");
            cut.Find("[data-testid='run-total-violations']").TextContent.Should().Contain("1");
        });
    }

    [Fact]
    public async Task RunRuleAnalysis_ShouldRenderProgressWhileRunIsActive()
    {
        var solution = RuleAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var runService = new RuleAnalysisTestRunService
        {
            PendingRun = new TaskCompletionSource<RuleAnalysisRunDto>(TaskCreationOptions.RunContinuationsAsynchronously)
        };

        Services.AddSingleton<IRegisteredSolutionService>(new RuleAnalysisTestRegisteredSolutionService(solution));
        Services.AddSingleton<ISolutionRuleSelectionService>(new RuleAnalysisTestSelectionService());
        Services.AddSingleton<IRuleAnalysisRunService>(runService);
        Services.AddSingleton<IRuleAnalysisResultService>(new RuleAnalysisTestResultService());

        var cut = RenderComponent<RuleAnalysis>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='run-rule-analysis-button']").TextContent.Should().Contain("Run rule analysis"));

        var clickTask = cut.Find("[data-testid='run-rule-analysis-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rule-analysis-progress']");
            cut.Find("[data-testid='rule-analysis-progress-stage']").TextContent.Should().Contain("Analyzing projects");
            cut.Find("[data-testid='rule-analysis-progress-message']").TextContent.Should().Contain("Analyzing source documents");
            cut.Find("[data-testid='rule-analysis-progress-detail']").TextContent.Should().Contain("src/App/Program.cs");
            cut.Find("[data-testid='rule-analysis-progress-count']").TextContent.Should().Contain("1 / 2");
            cut.Find("[data-testid='rule-analysis-progress-bar']").GetAttribute("aria-valuenow").Should().Be("50");
        });

        runService.PendingRun.SetResult(RuleAnalysisComponentTestData.CreateSucceededRun(solution.Id));
        await clickTask;

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='run-rule-analysis-button']").TextContent.Should().Contain("Run rule analysis");
        });
    }

    [Fact]
    public async Task SwitchingSolutions_ShouldReloadLatestResultsForSelectedSolution()
    {
        var alpha = RuleAnalysisComponentTestData.CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var beta = RuleAnalysisComponentTestData.CreateSolution("Beta", "/solutions/beta.sln", RegisteredSolutionStatus.Valid);
        var resultService = new RuleAnalysisTestResultService();
        resultService.LatestRunsBySolution[alpha.Id] = RuleAnalysisComponentTestData.CreateSucceededRun(alpha.Id, "Alpha latest", totalViolations: 1);
        resultService.LatestRunsBySolution[beta.Id] = RuleAnalysisComponentTestData.CreateSucceededRun(beta.Id, "Beta latest", totalViolations: 1);

        Services.AddSingleton<IRegisteredSolutionService>(new RuleAnalysisTestRegisteredSolutionService(alpha, beta));
        Services.AddSingleton<ISolutionRuleSelectionService>(new RuleAnalysisTestSelectionService());
        Services.AddSingleton<IRuleAnalysisRunService>(new RuleAnalysisTestRunService());
        Services.AddSingleton<IRuleAnalysisResultService>(resultService);

        var cut = RenderComponent<RuleAnalysis>();

        cut.WaitForAssertion(() =>
        {
            resultService.LatestCalls.Should().Contain(alpha.Id);
            cut.Markup.Should().Contain("Alpha latest");
        });

        await cut.FindAll("[data-testid='select-solution-button']")[1].ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            resultService.LatestCalls.Should().Contain(beta.Id);
            cut.Markup.Should().Contain("Beta latest");
            cut.Markup.Should().NotContain("Alpha latest");
        });
    }
}

internal static class RuleAnalysisComponentTestData
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

    public static SolutionRuleSelectionDto CreateSelection(
        string code = "CP1000",
        string title = "Avoid Console.WriteLine",
        RuleSeverity severity = RuleSeverity.Warning,
        string kind = "forbidden_api_usage",
        bool isGloballyEnabled = true,
        bool isEnabledForSolution = false,
        Guid? ruleId = null)
        => new(
            ruleId ?? Guid.NewGuid(),
            code,
            title,
            severity,
            kind,
            isGloballyEnabled,
            isEnabledForSolution,
            DateTimeOffset.UtcNow);

    public static RuleAnalysisRunDto CreateSucceededRun(Guid solutionId, string ruleTitle = "Avoid Console.WriteLine", int totalViolations = 0)
        => new(
            Guid.NewGuid(),
            solutionId,
            RuleAnalysisRunStatus.Succeeded,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            1,
            totalViolations,
            null,
            totalViolations == 0
                ? []
                :
                [
                    new RuleAnalysisRuleGroupDto(
                        "CP1000",
                        ruleTitle,
                        "forbidden_api_usage",
                        RuleSeverity.Warning,
                        totalViolations,
                        [CreateViolation()])
                ]);

    public static RuleAnalysisRunDto CreateFailedRun(Guid solutionId, string errorMessage)
        => new(
            Guid.NewGuid(),
            solutionId,
            RuleAnalysisRunStatus.Failed,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            1,
            0,
            errorMessage,
            []);

    public static RuleAnalysisViolationDto CreateViolation(
        RuleSeverity severity = RuleSeverity.Error,
        string filePath = "src/App/Program.cs",
        int startLine = 12,
        int startColumn = 9,
        int endLine = 12,
        int endColumn = 27)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "CP1000",
            "Avoid Console.WriteLine",
            "forbidden_api_usage",
            severity,
            "Avoid calling Console.WriteLine directly.",
            filePath,
            startLine,
            startColumn,
            endLine,
            endColumn);
}

internal sealed class RuleAnalysisTestRegisteredSolutionService(params RegisteredSolution[] seededSolutions) : IRegisteredSolutionService
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

internal sealed class RuleAnalysisTestSelectionService : ISolutionRuleSelectionService
{
    public Dictionary<Guid, IReadOnlyList<SolutionRuleSelectionDto>> SelectionsBySolution { get; } = [];
    public List<Guid> GetSelectionCalls { get; } = [];
    public List<SetSolutionRuleSelectionRequest> SetRuleCalls { get; } = [];

    public Task<IReadOnlyList<SolutionRuleSelectionDto>> GetSelectionsAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        GetSelectionCalls.Add(registeredSolutionId);
        return Task.FromResult(SelectionsBySolution.GetValueOrDefault(registeredSolutionId, []));
    }

    public Task SetRuleEnabledAsync(SetSolutionRuleSelectionRequest request, CancellationToken cancellationToken = default)
    {
        SetRuleCalls.Add(request);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetEnabledRuleDefinitionsForSolutionAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuthoredRuleDefinitionDto>>([]);
}

internal sealed class RuleAnalysisTestRunService : IRuleAnalysisRunService
{
    public Dictionary<Guid, RuleAnalysisRunDto> RunsBySolution { get; } = [];
    public List<Guid> StartCalls { get; } = [];
    public TaskCompletionSource<RuleAnalysisRunDto>? PendingRun { get; set; }

    public Task<RuleAnalysisRunDto> StartRunAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default,
        IProgress<RuleAnalysisProgressDto>? progress = null)
    {
        StartCalls.Add(registeredSolutionId);
        progress?.Report(new RuleAnalysisProgressDto(
            RuleAnalysisProgressStage.AnalyzingProjects,
            "Analyzing source documents...",
            PercentComplete: 50,
            Current: 1,
            Total: 2,
            Detail: "src/App/Program.cs"));

        if (PendingRun is not null)
        {
            return PendingRun.Task;
        }

        return Task.FromResult(RunsBySolution.GetValueOrDefault(registeredSolutionId, RuleAnalysisComponentTestData.CreateSucceededRun(registeredSolutionId)));
    }
}

internal sealed class RuleAnalysisTestResultService : IRuleAnalysisResultService
{
    public Dictionary<Guid, RuleAnalysisRunDto?> LatestRunsBySolution { get; } = [];
    public List<Guid> LatestCalls { get; } = [];

    public Task<RuleAnalysisRunDto> CreateRunningRunAsync(Guid registeredSolutionId, int ruleCount, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RuleAnalysisRunDto> MarkSucceededAsync(Guid runId, IReadOnlyList<RuleAnalysisFinding> findings, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RuleAnalysisRunDto> MarkFailedAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RuleAnalysisRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RuleAnalysisRunDto?> GetLatestRunForSolutionAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        LatestCalls.Add(registeredSolutionId);
        return Task.FromResult(LatestRunsBySolution.GetValueOrDefault(registeredSolutionId));
    }
}
