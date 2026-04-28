using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.RuleAnalysis;
using CodePass.Web.Services.Rules;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Services;

public sealed class RuleAnalysisRunServiceTests
{
    [Fact]
    public async Task StartRunAsync_ShouldPassOnlyEnabledRulesForTheSelectedSolutionToAnalyzer()
    {
        await using var fixture = await RuleAnalysisRunServiceFixture.CreateAsync();

        var run = await fixture.Service.StartRunAsync(fixture.SolutionA.Id);

        run.Status.Should().Be(RuleAnalysisRunStatus.Succeeded);
        run.RuleCount.Should().Be(1);
        var call = fixture.Analyzer.Calls.Should().ContainSingle().Subject;
        call.SolutionPath.Should().Be(fixture.SolutionA.SolutionPath);
        call.Rules.Select(rule => rule.Code).Should().Equal(fixture.EnabledRule.Code);
        call.Rules.Should().NotContain(rule => rule.Id == fixture.GloballyDisabledRule.Id);
        call.Rules.Should().NotContain(rule => rule.Id == fixture.DisabledForSolutionRule.Id);
        call.Rules.Should().NotContain(rule => rule.Id == fixture.SolutionBRule.Id);
    }

    [Fact]
    public async Task StartRunAsync_ShouldPersistSuccessfulAnalyzerFindingsGroupedByRule()
    {
        await using var fixture = await RuleAnalysisRunServiceFixture.CreateAsync();
        fixture.Analyzer.FindingsToReturn =
        [
            fixture.CreateFinding(fixture.EnabledRule, "src/Zeta.cs", startLine: 12),
            fixture.CreateFinding(fixture.EnabledRule, "src/Alpha.cs", startLine: 3)
        ];

        var run = await fixture.Service.StartRunAsync(fixture.SolutionA.Id);

        run.Status.Should().Be(RuleAnalysisRunStatus.Succeeded);
        run.TotalViolations.Should().Be(2);
        run.RuleGroups.Should().ContainSingle();
        var group = run.RuleGroups[0];
        group.RuleCode.Should().Be(fixture.EnabledRule.Code);
        group.RuleTitle.Should().Be(fixture.EnabledRule.Title);
        group.ViolationCount.Should().Be(2);
        group.Violations.Select(violation => violation.FilePath).Should().Equal("src/Alpha.cs", "src/Zeta.cs");

        var persistedRun = await fixture.DbContext.RuleAnalysisRuns
            .Include(existing => existing.Violations)
            .SingleAsync(existing => existing.Id == run.Id);
        persistedRun.Status.Should().Be(RuleAnalysisRunStatus.Succeeded);
        persistedRun.Violations.Should().HaveCount(2);
    }

    [Fact]
    public async Task StartRunAsync_ShouldSucceedWithZeroViolationsWhenNoRulesAreEnabled()
    {
        await using var fixture = await RuleAnalysisRunServiceFixture.CreateAsync();
        fixture.DbContext.SolutionRuleAssignments.RemoveRange(fixture.DbContext.SolutionRuleAssignments);
        await fixture.DbContext.SaveChangesAsync();

        var run = await fixture.Service.StartRunAsync(fixture.SolutionA.Id);

        run.Status.Should().Be(RuleAnalysisRunStatus.Succeeded);
        run.CompletedAtUtc.Should().NotBeNull();
        run.RuleCount.Should().Be(0);
        run.TotalViolations.Should().Be(0);
        run.RuleGroups.Should().BeEmpty();
        fixture.Analyzer.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task StartRunAsync_ShouldCreateFailedRunForNonValidSolutionWithoutCallingAnalyzer()
    {
        await using var fixture = await RuleAnalysisRunServiceFixture.CreateAsync();

        var run = await fixture.Service.StartRunAsync(fixture.InvalidSolution.Id);

        run.Status.Should().Be(RuleAnalysisRunStatus.Failed);
        run.RuleCount.Should().Be(0);
        run.ErrorMessage.Should().Contain("Only valid registered solutions can be analyzed");
        run.ErrorMessage.Should().Contain(RegisteredSolutionStatus.FileNotFound.ToString());
        run.RuleGroups.Should().BeEmpty();
        fixture.Analyzer.Calls.Should().BeEmpty();

        var persistedRun = await fixture.DbContext.RuleAnalysisRuns.SingleAsync(existing => existing.Id == run.Id);
        persistedRun.RegisteredSolutionId.Should().Be(fixture.InvalidSolution.Id);
        persistedRun.Status.Should().Be(RuleAnalysisRunStatus.Failed);
    }

    [Fact]
    public async Task StartRunAsync_ShouldCreateFailedRunWhenAnalyzerThrows()
    {
        await using var fixture = await RuleAnalysisRunServiceFixture.CreateAsync();
        fixture.Analyzer.ExceptionToThrow = new InvalidOperationException("MSBuild could not load the solution.");

        var run = await fixture.Service.StartRunAsync(fixture.SolutionA.Id);

        run.Status.Should().Be(RuleAnalysisRunStatus.Failed);
        run.ErrorMessage.Should().Be("MSBuild could not load the solution.");
        run.TotalViolations.Should().Be(0);
        run.RuleGroups.Should().BeEmpty();
        fixture.Analyzer.Calls.Should().ContainSingle();

        var persistedRun = await fixture.DbContext.RuleAnalysisRuns.SingleAsync(existing => existing.Id == run.Id);
        persistedRun.Status.Should().Be(RuleAnalysisRunStatus.Failed);
        persistedRun.ErrorMessage.Should().Be("MSBuild could not load the solution.");
    }

    [Fact]
    public async Task ServiceProvider_ShouldResolveManualRunServiceWithRealScopedDependencies()
    {
        var databaseName = $"codepass-rule-analysis-run-di-{Guid.NewGuid():N}";
        var fakeAnalyzer = new FakeRuleAnalyzer();
        var now = DateTimeOffset.UtcNow;
        var solution = RuleAnalysisRunServiceFixture.CreateSolution("DI Solution", "/tmp/di-solution.sln", RegisteredSolutionStatus.Valid, now);
        var rule = RuleAnalysisRunServiceFixture.CreateRule("CP-DI", "DI rule", isEnabled: true, RuleSeverity.Warning, now);

        var services = new ServiceCollection();
        services.AddDbContext<CodePassDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<ISolutionRuleSelectionService, SolutionRuleSelectionService>();
        services.AddScoped<IRuleAnalysisResultService, RuleAnalysisResultService>();
        services.AddScoped<IRuleAnalyzer>(_ => fakeAnalyzer);
        services.AddScoped<IRuleAnalysisRunService, RuleAnalysisRunService>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using (var seedScope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<CodePassDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.RegisteredSolutions.Add(solution);
            dbContext.AuthoredRuleDefinitions.Add(rule);
            dbContext.SolutionRuleAssignments.Add(RuleAnalysisRunServiceFixture.CreateAssignment(solution.Id, rule.Id, isEnabled: true, now));
            await dbContext.SaveChangesAsync();
        }

        await using var runScope = serviceProvider.CreateAsyncScope();
        var runService = runScope.ServiceProvider.GetRequiredService<IRuleAnalysisRunService>();

        var run = await runService.StartRunAsync(solution.Id);

        run.Status.Should().Be(RuleAnalysisRunStatus.Succeeded);
        fakeAnalyzer.Calls.Should().ContainSingle();
    }
}

internal sealed class RuleAnalysisRunServiceFixture : IAsyncDisposable
{
    private RuleAnalysisRunServiceFixture(
        CodePassDbContext dbContext,
        IRuleAnalysisRunService service,
        FakeRuleAnalyzer analyzer,
        RegisteredSolution solutionA,
        RegisteredSolution solutionB,
        RegisteredSolution invalidSolution,
        AuthoredRuleDefinition enabledRule,
        AuthoredRuleDefinition globallyDisabledRule,
        AuthoredRuleDefinition disabledForSolutionRule,
        AuthoredRuleDefinition solutionBRule)
    {
        DbContext = dbContext;
        Service = service;
        Analyzer = analyzer;
        SolutionA = solutionA;
        SolutionB = solutionB;
        InvalidSolution = invalidSolution;
        EnabledRule = enabledRule;
        GloballyDisabledRule = globallyDisabledRule;
        DisabledForSolutionRule = disabledForSolutionRule;
        SolutionBRule = solutionBRule;
    }

    public CodePassDbContext DbContext { get; }

    public IRuleAnalysisRunService Service { get; }

    public FakeRuleAnalyzer Analyzer { get; }

    public RegisteredSolution SolutionA { get; }

    public RegisteredSolution SolutionB { get; }

    public RegisteredSolution InvalidSolution { get; }

    public AuthoredRuleDefinition EnabledRule { get; }

    public AuthoredRuleDefinition GloballyDisabledRule { get; }

    public AuthoredRuleDefinition DisabledForSolutionRule { get; }

    public AuthoredRuleDefinition SolutionBRule { get; }

    public static async Task<RuleAnalysisRunServiceFixture> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseInMemoryDatabase($"codepass-rule-analysis-run-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var solutionA = CreateSolution("Solution A", "/tmp/solution-a.sln", RegisteredSolutionStatus.Valid, now);
        var solutionB = CreateSolution("Solution B", "/tmp/solution-b.sln", RegisteredSolutionStatus.Valid, now);
        var invalidSolution = CreateSolution("Missing Solution", "/tmp/missing.sln", RegisteredSolutionStatus.FileNotFound, now);
        var enabledRule = CreateRule("CP3000", "Enabled for solution", isEnabled: true, RuleSeverity.Error, now);
        var globallyDisabledRule = CreateRule("CP3001", "Globally disabled", isEnabled: false, RuleSeverity.Warning, now);
        var disabledForSolutionRule = CreateRule("CP3002", "Disabled for solution", isEnabled: true, RuleSeverity.Warning, now);
        var solutionBRule = CreateRule("CP3003", "Enabled only for solution B", isEnabled: true, RuleSeverity.Info, now);

        dbContext.RegisteredSolutions.AddRange(solutionA, solutionB, invalidSolution);
        dbContext.AuthoredRuleDefinitions.AddRange(enabledRule, globallyDisabledRule, disabledForSolutionRule, solutionBRule);
        dbContext.SolutionRuleAssignments.AddRange(
            CreateAssignment(solutionA.Id, enabledRule.Id, isEnabled: true, now),
            CreateAssignment(solutionA.Id, globallyDisabledRule.Id, isEnabled: true, now),
            CreateAssignment(solutionA.Id, disabledForSolutionRule.Id, isEnabled: false, now),
            CreateAssignment(solutionB.Id, solutionBRule.Id, isEnabled: true, now));
        await dbContext.SaveChangesAsync();

        var analyzer = new FakeRuleAnalyzer();
        var service = new RuleAnalysisRunService(
            dbContext,
            new SolutionRuleSelectionService(dbContext),
            analyzer,
            new RuleAnalysisResultService(dbContext));

        return new RuleAnalysisRunServiceFixture(
            dbContext,
            service,
            analyzer,
            solutionA,
            solutionB,
            invalidSolution,
            enabledRule,
            globallyDisabledRule,
            disabledForSolutionRule,
            solutionBRule);
    }

    public RuleAnalysisFinding CreateFinding(
        AuthoredRuleDefinition rule,
        string relativeFilePath,
        int startLine = 1,
        int startColumn = 1) => new(
            rule.Id,
            rule.Code,
            rule.Title,
            rule.RuleKind,
            rule.Severity,
            $"{rule.Title} violation in {relativeFilePath}",
            relativeFilePath,
            startLine,
            startColumn,
            startLine,
            startColumn + 7);

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }

    public static RegisteredSolution CreateSolution(
        string displayName,
        string solutionPath,
        RegisteredSolutionStatus status,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = displayName,
        SolutionPath = solutionPath,
        Status = status,
        StatusMessage = status == RegisteredSolutionStatus.Valid ? null : "Solution file was not found.",
        LastValidatedAtUtc = now,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    public static AuthoredRuleDefinition CreateRule(
        string code,
        string title,
        bool isEnabled,
        RuleSeverity severity,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Title = title,
        Description = null,
        RuleKind = "forbidden_api_usage",
        SchemaVersion = "1.0",
        Severity = severity,
        ScopeJson = "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}",
        ParametersJson = "{\"forbiddenSymbols\":[\"System.Console.WriteLine\"],\"allowedAlternatives\":[\"ILogger.LogInformation\"],\"allowInTests\":false}",
        RawDefinitionJson = "{}",
        IsEnabled = isEnabled,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    public static SolutionRuleAssignment CreateAssignment(
        Guid registeredSolutionId,
        Guid authoredRuleDefinitionId,
        bool isEnabled,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        RegisteredSolutionId = registeredSolutionId,
        AuthoredRuleDefinitionId = authoredRuleDefinitionId,
        IsEnabled = isEnabled,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };
}

internal sealed class FakeRuleAnalyzer : IRuleAnalyzer
{
    public List<AnalyzeCall> Calls { get; } = [];

    public IReadOnlyList<RuleAnalysisFinding> FindingsToReturn { get; set; } = [];

    public Exception? ExceptionToThrow { get; set; }

    public Task<IReadOnlyList<RuleAnalysisFinding>> AnalyzeAsync(
        string solutionPath,
        IReadOnlyList<AuthoredRuleDefinitionDto> rules,
        CancellationToken cancellationToken = default,
        IProgress<RuleAnalysisProgressDto>? progress = null)
    {
        Calls.Add(new AnalyzeCall(solutionPath, rules.ToList()));

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(FindingsToReturn);
    }
}

internal sealed record AnalyzeCall(string SolutionPath, IReadOnlyList<AuthoredRuleDefinitionDto> Rules);
