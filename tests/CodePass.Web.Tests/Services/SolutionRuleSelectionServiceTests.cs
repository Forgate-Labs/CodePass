using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.RuleAnalysis;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Tests.Services;

public sealed class SolutionRuleSelectionServiceTests
{
    [Fact]
    public async Task GetSelectionsAsync_ShouldDefaultAuthoredRulesToDisabledWhenNoAssignmentsExist()
    {
        await using var fixture = await SolutionRuleSelectionServiceFixture.CreateAsync();

        var selections = await fixture.Service.GetSelectionsAsync(fixture.SolutionA.Id);

        selections.Should().HaveCount(2);
        selections.Select(selection => selection.RuleCode).Should().Equal("CP1001", "CP1000");
        selections.Should().OnlyContain(selection => !selection.IsEnabledForSolution);
        selections.Should().ContainSingle(selection => selection.RuleId == fixture.GloballyDisabledRule.Id && !selection.IsGloballyEnabled);
        fixture.DbContext.SolutionRuleAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task SetRuleEnabledAsync_ShouldEnableOnlyTheRequestedSolution()
    {
        await using var fixture = await SolutionRuleSelectionServiceFixture.CreateAsync();

        await fixture.Service.SetRuleEnabledAsync(new SetSolutionRuleSelectionRequest(
            fixture.SolutionA.Id,
            fixture.GloballyEnabledRule.Id,
            IsEnabled: true));

        var solutionASelections = await fixture.Service.GetSelectionsAsync(fixture.SolutionA.Id);
        var solutionBSelections = await fixture.Service.GetSelectionsAsync(fixture.SolutionB.Id);

        solutionASelections.Single(selection => selection.RuleId == fixture.GloballyEnabledRule.Id).IsEnabledForSolution.Should().BeTrue();
        solutionBSelections.Single(selection => selection.RuleId == fixture.GloballyEnabledRule.Id).IsEnabledForSolution.Should().BeFalse();
        fixture.DbContext.SolutionRuleAssignments.Should().ContainSingle();
    }

    [Fact]
    public async Task SetRuleEnabledAsync_ShouldUpdateExistingAssignmentInsteadOfDuplicatingRows()
    {
        await using var fixture = await SolutionRuleSelectionServiceFixture.CreateAsync();

        await fixture.Service.SetRuleEnabledAsync(new SetSolutionRuleSelectionRequest(
            fixture.SolutionA.Id,
            fixture.GloballyEnabledRule.Id,
            IsEnabled: true));
        var assignmentId = await fixture.DbContext.SolutionRuleAssignments
            .Select(assignment => assignment.Id)
            .SingleAsync();

        await fixture.Service.SetRuleEnabledAsync(new SetSolutionRuleSelectionRequest(
            fixture.SolutionA.Id,
            fixture.GloballyEnabledRule.Id,
            IsEnabled: false));

        var assignment = await fixture.DbContext.SolutionRuleAssignments.SingleAsync();
        assignment.Id.Should().Be(assignmentId);
        assignment.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetEnabledRuleDefinitionsForSolutionAsync_ShouldRequireBothGlobalAndSolutionEnabledState()
    {
        await using var fixture = await SolutionRuleSelectionServiceFixture.CreateAsync();

        await fixture.Service.SetRuleEnabledAsync(new SetSolutionRuleSelectionRequest(
            fixture.SolutionA.Id,
            fixture.GloballyEnabledRule.Id,
            IsEnabled: true));
        await fixture.Service.SetRuleEnabledAsync(new SetSolutionRuleSelectionRequest(
            fixture.SolutionA.Id,
            fixture.GloballyDisabledRule.Id,
            IsEnabled: true));

        var enabledRules = await fixture.Service.GetEnabledRuleDefinitionsForSolutionAsync(fixture.SolutionA.Id);

        enabledRules.Should().ContainSingle();
        enabledRules[0].Id.Should().Be(fixture.GloballyEnabledRule.Id);
        enabledRules[0].Code.Should().Be("CP1000");
    }

    [Fact]
    public async Task SetRuleEnabledAsync_ShouldThrowClearExceptionForUnknownSolutionOrRule()
    {
        await using var fixture = await SolutionRuleSelectionServiceFixture.CreateAsync();

        var missingSolution = async () => await fixture.Service.SetRuleEnabledAsync(new SetSolutionRuleSelectionRequest(
            Guid.NewGuid(),
            fixture.GloballyEnabledRule.Id,
            IsEnabled: true));
        var missingRule = async () => await fixture.Service.SetRuleEnabledAsync(new SetSolutionRuleSelectionRequest(
            fixture.SolutionA.Id,
            Guid.NewGuid(),
            IsEnabled: true));

        await missingSolution.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Registered solution*not found*");
        await missingRule.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Authored rule definition*not found*");
        fixture.DbContext.SolutionRuleAssignments.Should().BeEmpty();
    }
}

internal sealed class SolutionRuleSelectionServiceFixture : IAsyncDisposable
{
    private SolutionRuleSelectionServiceFixture(
        CodePassDbContext dbContext,
        ISolutionRuleSelectionService service,
        RegisteredSolution solutionA,
        RegisteredSolution solutionB,
        AuthoredRuleDefinition globallyEnabledRule,
        AuthoredRuleDefinition globallyDisabledRule)
    {
        DbContext = dbContext;
        Service = service;
        SolutionA = solutionA;
        SolutionB = solutionB;
        GloballyEnabledRule = globallyEnabledRule;
        GloballyDisabledRule = globallyDisabledRule;
    }

    public CodePassDbContext DbContext { get; }

    public ISolutionRuleSelectionService Service { get; }

    public RegisteredSolution SolutionA { get; }

    public RegisteredSolution SolutionB { get; }

    public AuthoredRuleDefinition GloballyEnabledRule { get; }

    public AuthoredRuleDefinition GloballyDisabledRule { get; }

    public static async Task<SolutionRuleSelectionServiceFixture> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseInMemoryDatabase($"codepass-rule-selection-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var solutionA = CreateSolution("Solution A", "/tmp/solution-a.sln", now);
        var solutionB = CreateSolution("Solution B", "/tmp/solution-b.sln", now);
        var globallyEnabledRule = CreateRule("CP1000", "Zeta enabled rule", isEnabled: true, now);
        var globallyDisabledRule = CreateRule("CP1001", "Alpha globally disabled rule", isEnabled: false, now);

        dbContext.RegisteredSolutions.AddRange(solutionA, solutionB);
        dbContext.AuthoredRuleDefinitions.AddRange(globallyEnabledRule, globallyDisabledRule);
        await dbContext.SaveChangesAsync();

        return new SolutionRuleSelectionServiceFixture(
            dbContext,
            new SolutionRuleSelectionService(dbContext),
            solutionA,
            solutionB,
            globallyEnabledRule,
            globallyDisabledRule);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
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

    private static AuthoredRuleDefinition CreateRule(string code, string title, bool isEnabled, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Title = title,
        Description = null,
        RuleKind = "forbidden_api_usage",
        SchemaVersion = "1.0",
        Severity = RuleSeverity.Warning,
        ScopeJson = "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}",
        ParametersJson = "{\"forbiddenSymbols\":[\"System.Console.WriteLine\"],\"allowedAlternatives\":[\"ILogger.LogInformation\"],\"allowInTests\":false}",
        RawDefinitionJson = "{\"id\":\"" + code + "\",\"title\":\"" + title + "\",\"kind\":\"forbidden_api_usage\",\"schemaVersion\":\"1.0\",\"severity\":\"warning\",\"enabled\":" + isEnabled.ToString().ToLowerInvariant() + ",\"language\":\"csharp\",\"scope\":{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]},\"parameters\":{\"forbiddenSymbols\":[\"System.Console.WriteLine\"],\"allowedAlternatives\":[\"ILogger.LogInformation\"],\"allowInTests\":false}}",
        IsEnabled = isEnabled,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };
}
