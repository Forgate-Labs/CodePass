using System.Data.Common;
using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.RuleAnalysis;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Tests.Services;

public sealed class RuleAnalysisResultServiceTests
{
    [Fact]
    public async Task GetLatestRunForSolutionAsync_ShouldReturnNewestRunOnlyForRequestedSolution()
    {
        await using var fixture = await RuleAnalysisResultServiceFixture.CreateAsync();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        var olderSolutionARun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id, ruleCount: 2);
        await SetStartedAtAsync(fixture.DbContext, olderSolutionARun.Id, baseTime);
        await fixture.Service.MarkSucceededAsync(olderSolutionARun.Id, [fixture.CreateFinding(fixture.ErrorRule, "src/Old.cs")]);

        var solutionBRun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionB.Id, ruleCount: 1);
        await SetStartedAtAsync(fixture.DbContext, solutionBRun.Id, baseTime.AddMinutes(1));
        await fixture.Service.MarkSucceededAsync(solutionBRun.Id, [fixture.CreateFinding(fixture.WarningRule, "src/Other.cs")]);

        var newestSolutionARun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id, ruleCount: 2);
        await SetStartedAtAsync(fixture.DbContext, newestSolutionARun.Id, baseTime.AddMinutes(2));
        await fixture.Service.MarkSucceededAsync(newestSolutionARun.Id, []);

        var latestSolutionA = await fixture.Service.GetLatestRunForSolutionAsync(fixture.SolutionA.Id);
        var latestSolutionB = await fixture.Service.GetLatestRunForSolutionAsync(fixture.SolutionB.Id);

        latestSolutionA.Should().NotBeNull();
        latestSolutionA!.Id.Should().Be(newestSolutionARun.Id);
        latestSolutionA.RegisteredSolutionId.Should().Be(fixture.SolutionA.Id);
        latestSolutionA.TotalViolations.Should().Be(0);
        latestSolutionB.Should().NotBeNull();
        latestSolutionB!.Id.Should().Be(solutionBRun.Id);
        latestSolutionB.RegisteredSolutionId.Should().Be(fixture.SolutionB.Id);
    }

    [Fact]
    public async Task GetLatestRunForSolutionAsync_ShouldOrderRunsWithSqliteProvider()
    {
        await using var fixture = await RuleAnalysisResultServiceFixture.CreateSqliteAsync();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        var olderRun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id, ruleCount: 1);
        await SetStartedAtAsync(fixture.DbContext, olderRun.Id, baseTime);
        await fixture.Service.MarkSucceededAsync(olderRun.Id, []);

        var newestRun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id, ruleCount: 1);
        await SetStartedAtAsync(fixture.DbContext, newestRun.Id, baseTime.AddMinutes(5));
        await fixture.Service.MarkSucceededAsync(newestRun.Id, []);

        var latestRun = await fixture.Service.GetLatestRunForSolutionAsync(fixture.SolutionA.Id);

        latestRun.Should().NotBeNull();
        latestRun!.Id.Should().Be(newestRun.Id);
    }

    [Fact]
    public async Task MarkSucceededAsync_ShouldPersistViolationsGroupedByRuleSnapshot()
    {
        await using var fixture = await RuleAnalysisResultServiceFixture.CreateAsync();
        var run = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id, ruleCount: 2);

        var completedRun = await fixture.Service.MarkSucceededAsync(run.Id,
        [
            fixture.CreateFinding(fixture.WarningRule, "src/Zeta.cs", startLine: 20, startColumn: 8),
            fixture.CreateFinding(fixture.ErrorRule, "src/Beta.cs", startLine: 12, startColumn: 4),
            fixture.CreateFinding(fixture.ErrorRule, "src/Alpha.cs", startLine: 5, startColumn: 2)
        ]);

        completedRun.Status.Should().Be(RuleAnalysisRunStatus.Succeeded);
        completedRun.CompletedAtUtc.Should().NotBeNull();
        completedRun.RuleCount.Should().Be(2);
        completedRun.TotalViolations.Should().Be(3);
        completedRun.RuleGroups.Should().HaveCount(2);
        completedRun.RuleGroups.Select(group => group.RuleCode).Should().Equal("CP2000", "CP2001");

        var errorGroup = completedRun.RuleGroups[0];
        errorGroup.RuleTitle.Should().Be("Avoid console writes");
        errorGroup.RuleKind.Should().Be("forbidden_api_usage");
        errorGroup.Severity.Should().Be(RuleSeverity.Error);
        errorGroup.ViolationCount.Should().Be(2);
        errorGroup.Violations.Select(violation => violation.FilePath).Should().Equal("src/Alpha.cs", "src/Beta.cs");

        var firstViolation = errorGroup.Violations[0];
        firstViolation.AuthoredRuleDefinitionId.Should().Be(fixture.ErrorRule.Id);
        firstViolation.Severity.Should().Be(RuleSeverity.Error);
        firstViolation.FilePath.Should().Be("src/Alpha.cs");
        firstViolation.StartLine.Should().Be(5);
        firstViolation.StartColumn.Should().Be(2);
        firstViolation.EndLine.Should().Be(5);
        firstViolation.EndColumn.Should().Be(12);
        firstViolation.Message.Should().Be("Avoid console writes violation in src/Alpha.cs");
    }

    [Fact]
    public async Task MarkFailedAsync_ShouldPreserveErrorMessageAndNoViolations()
    {
        await using var fixture = await RuleAnalysisResultServiceFixture.CreateAsync();
        var run = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id, ruleCount: 2);

        var failedRun = await fixture.Service.MarkFailedAsync(run.Id, "MSBuild failed to load the solution.");
        var persistedRun = await fixture.Service.GetRunAsync(run.Id);

        failedRun.Status.Should().Be(RuleAnalysisRunStatus.Failed);
        failedRun.ErrorMessage.Should().Be("MSBuild failed to load the solution.");
        failedRun.TotalViolations.Should().Be(0);
        failedRun.RuleGroups.Should().BeEmpty();
        persistedRun.Should().NotBeNull();
        persistedRun!.Status.Should().Be(RuleAnalysisRunStatus.Failed);
        persistedRun.ErrorMessage.Should().Be("MSBuild failed to load the solution.");
        persistedRun.RuleGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkSucceededAsync_ShouldPersistZeroViolationRuns()
    {
        await using var fixture = await RuleAnalysisResultServiceFixture.CreateAsync();
        var run = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id, ruleCount: 2);

        await fixture.Service.MarkSucceededAsync(run.Id, []);
        var persistedRun = await fixture.Service.GetRunAsync(run.Id);

        persistedRun.Should().NotBeNull();
        persistedRun!.Status.Should().Be(RuleAnalysisRunStatus.Succeeded);
        persistedRun.CompletedAtUtc.Should().NotBeNull();
        persistedRun.TotalViolations.Should().Be(0);
        persistedRun.RuleGroups.Should().BeEmpty();
    }

    private static async Task SetStartedAtAsync(CodePassDbContext dbContext, Guid runId, DateTimeOffset startedAtUtc)
    {
        var run = await dbContext.RuleAnalysisRuns.SingleAsync(existing => existing.Id == runId);
        run.StartedAtUtc = startedAtUtc;
        await dbContext.SaveChangesAsync();
    }
}

internal sealed class RuleAnalysisResultServiceFixture : IAsyncDisposable
{
    private RuleAnalysisResultServiceFixture(
        CodePassDbContext dbContext,
        IRuleAnalysisResultService service,
        RegisteredSolution solutionA,
        RegisteredSolution solutionB,
        AuthoredRuleDefinition errorRule,
        AuthoredRuleDefinition warningRule,
        DbConnection? dbConnection = null)
    {
        DbContext = dbContext;
        Service = service;
        SolutionA = solutionA;
        SolutionB = solutionB;
        ErrorRule = errorRule;
        WarningRule = warningRule;
        DbConnection = dbConnection;
    }

    public CodePassDbContext DbContext { get; }

    public IRuleAnalysisResultService Service { get; }

    public RegisteredSolution SolutionA { get; }

    public RegisteredSolution SolutionB { get; }

    public AuthoredRuleDefinition ErrorRule { get; }

    public AuthoredRuleDefinition WarningRule { get; }

    private DbConnection? DbConnection { get; }

    public static async Task<RuleAnalysisResultServiceFixture> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseInMemoryDatabase($"codepass-rule-analysis-results-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var solutionA = CreateSolution("Solution A", "/tmp/solution-a.sln", now);
        var solutionB = CreateSolution("Solution B", "/tmp/solution-b.sln", now);
        var errorRule = CreateRule("CP2000", "Avoid console writes", RuleSeverity.Error, now);
        var warningRule = CreateRule("CP2001", "Avoid var", RuleSeverity.Warning, now);

        dbContext.RegisteredSolutions.AddRange(solutionA, solutionB);
        dbContext.AuthoredRuleDefinitions.AddRange(errorRule, warningRule);
        await dbContext.SaveChangesAsync();

        return new RuleAnalysisResultServiceFixture(
            dbContext,
            new RuleAnalysisResultService(dbContext),
            solutionA,
            solutionB,
            errorRule,
            warningRule);
    }

    public static async Task<RuleAnalysisResultServiceFixture> CreateSqliteAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var solutionA = CreateSolution("Solution A", "/tmp/solution-a.sln", now);
        var solutionB = CreateSolution("Solution B", "/tmp/solution-b.sln", now);
        var errorRule = CreateRule("CP2000", "Avoid console writes", RuleSeverity.Error, now);
        var warningRule = CreateRule("CP2001", "Avoid var", RuleSeverity.Warning, now);

        dbContext.RegisteredSolutions.AddRange(solutionA, solutionB);
        dbContext.AuthoredRuleDefinitions.AddRange(errorRule, warningRule);
        await dbContext.SaveChangesAsync();

        return new RuleAnalysisResultServiceFixture(
            dbContext,
            new RuleAnalysisResultService(dbContext),
            solutionA,
            solutionB,
            errorRule,
            warningRule,
            connection);
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
            startColumn + 10);

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();

        if (DbConnection is not null)
        {
            await DbConnection.DisposeAsync();
        }
    }

    private static RegisteredSolution CreateSolution(string displayName, string solutionPath, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = displayName,
        SolutionPath = solutionPath,
        Status = RegisteredSolutionStatus.Valid,
        LastValidatedAtUtc = now,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    private static AuthoredRuleDefinition CreateRule(string code, string title, RuleSeverity severity, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Title = title,
        Description = null,
        RuleKind = code == "CP2000" ? "forbidden_api_usage" : "syntax_presence",
        SchemaVersion = "1.0",
        Severity = severity,
        ScopeJson = "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}",
        ParametersJson = "{}",
        RawDefinitionJson = "{}",
        IsEnabled = true,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };
}
