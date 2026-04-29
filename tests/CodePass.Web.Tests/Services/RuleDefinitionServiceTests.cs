using System.Text.Json;
using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Rules;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Services;

public sealed class RuleDefinitionServiceTests
{
    [Fact]
    public async Task CreateAndUpdateAsync_ShouldNormalizeJsonAndPersistRawDslDocument()
    {
        await using var fixture = await RuleDefinitionServiceFixture.CreateAsync();

        var created = await fixture.Service.CreateAsync(new SaveAuthoredRuleDefinitionRequest(
            Code: "CP0001",
            Title: "  Ban Console WriteLine  ",
            Description: "  Use structured logging instead.  ",
            RuleKind: "forbidden_api_usage",
            SchemaVersion: "1.0",
            Severity: RuleSeverity.Warning,
            ScopeJson: "{ \"files\" : [ \"**/*.cs\" ], \"projects\" : [ \"*\" ], \"excludeFiles\" : [ \"**/*.g.cs\" ] }",
            ParametersJson: "{ \"forbiddenSymbols\" : [ \"System.Console.WriteLine\" ], \"allowedAlternatives\" : [ \"ILogger.LogInformation\" ], \"allowInTests\" : false }",
            IsEnabled: true));

        var updated = await fixture.Service.UpdateAsync(created.Id, new SaveAuthoredRuleDefinitionRequest(
            Code: "CP0001",
            Title: "Ban Console WriteLine",
            Description: "Use structured logging instead.",
            RuleKind: "forbidden_api_usage",
            SchemaVersion: "1.0",
            Severity: RuleSeverity.Error,
            ScopeJson: "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}",
            ParametersJson: "{\"forbiddenSymbols\":[\"System.Console.WriteLine\"],\"allowedAlternatives\":[\"ILogger.LogInformation\",\"ILogger.LogWarning\"],\"allowInTests\":false}",
            IsEnabled: true));

        created.Title.Should().Be("Ban Console WriteLine");
        created.Description.Should().Be("Use structured logging instead.");
        created.ScopeJson.Should().Be("{\"files\":[\"**/*.cs\"],\"projects\":[\"*\"],\"excludeFiles\":[\"**/*.g.cs\"]}");
        created.ParametersJson.Should().Be("{\"forbiddenSymbols\":[\"System.Console.WriteLine\"],\"allowedAlternatives\":[\"ILogger.LogInformation\"],\"allowInTests\":false}");

        updated.Severity.Should().Be(RuleSeverity.Error);
        updated.RawDefinitionJson.Should().Contain("\"id\":\"CP0001\"");
        updated.RawDefinitionJson.Should().Contain("\"kind\":\"forbidden_api_usage\"");
        updated.RawDefinitionJson.Should().Contain("\"severity\":\"error\"");

        var persisted = await fixture.DbContext.AuthoredRuleDefinitions.SingleAsync();
        persisted.RawDefinitionJson.Should().Be(updated.RawDefinitionJson);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectUnsupportedKindAndSchemaMismatch()
    {
        await using var fixture = await RuleDefinitionServiceFixture.CreateAsync();

        var invalidKind = async () => await fixture.Service.CreateAsync(new SaveAuthoredRuleDefinitionRequest(
            Code: "CP0002",
            Title: "Invalid kind",
            Description: null,
            RuleKind: "made_up_kind",
            SchemaVersion: "1.0",
            Severity: RuleSeverity.Warning,
            ScopeJson: ValidScopeJson,
            ParametersJson: ValidForbiddenApiParametersJson,
            IsEnabled: true));

        var invalidSchema = async () => await fixture.Service.CreateAsync(new SaveAuthoredRuleDefinitionRequest(
            Code: "CP0003",
            Title: "Invalid schema",
            Description: null,
            RuleKind: "forbidden_api_usage",
            SchemaVersion: "2.0",
            Severity: RuleSeverity.Warning,
            ScopeJson: ValidScopeJson,
            ParametersJson: ValidForbiddenApiParametersJson,
            IsEnabled: true));

        await invalidKind.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not supported*");
        await invalidSchema.Should().ThrowAsync<InvalidOperationException>().WithMessage("*schema version '1.0'*");
        fixture.DbContext.AuthoredRuleDefinitions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveRulesAsync_ShouldReturnOnlyEnabledDatabaseBackedRules()
    {
        await using var fixture = await RuleDefinitionServiceFixture.CreateAsync();

        await fixture.Service.CreateAsync(new SaveAuthoredRuleDefinitionRequest(
            Code: "CP1000",
            Title: "Zeta naming",
            Description: null,
            RuleKind: "symbol_naming",
            SchemaVersion: "1.0",
            Severity: RuleSeverity.Info,
            ScopeJson: ValidScopeJson,
            ParametersJson: ValidSymbolNamingParametersJson,
            IsEnabled: false));

        await fixture.Service.CreateAsync(new SaveAuthoredRuleDefinitionRequest(
            Code: "CP1001",
            Title: "Alpha API usage",
            Description: null,
            RuleKind: "forbidden_api_usage",
            SchemaVersion: "1.0",
            Severity: RuleSeverity.Warning,
            ScopeJson: ValidScopeJson,
            ParametersJson: ValidForbiddenApiParametersJson,
            IsEnabled: true));

        var activeRules = await fixture.Service.GetActiveRulesAsync();

        activeRules.Should().HaveCount(1);
        activeRules[0].Code.Should().Be("CP1001");
        activeRules[0].RuleKind.Should().Be("forbidden_api_usage");
        fixture.DbContext.AuthoredRuleDefinitions.Count().Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_ShouldAcceptNumberFieldsForMethodMetricsRules()
    {
        await using var fixture = await RuleDefinitionServiceFixture.CreateAsync();

        var created = await fixture.Service.CreateAsync(new SaveAuthoredRuleDefinitionRequest(
            Code: "CP2000",
            Title: "Limit method metrics",
            Description: null,
            RuleKind: "method_metrics",
            SchemaVersion: "1.0",
            Severity: RuleSeverity.Warning,
            ScopeJson: ValidScopeJson,
            ParametersJson: "{\"maxLines\":50,\"maxParameters\":5,\"maxCyclomaticComplexity\":10}",
            IsEnabled: true));

        created.RuleKind.Should().Be("method_metrics");
        created.ParametersJson.Should().Be("{\"maxLines\":50,\"maxParameters\":5,\"maxCyclomaticComplexity\":10}");
    }

    [Fact]
    public async Task RuleServices_ShouldResolveFromDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CodePassDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddScoped<IRuleCatalogService, RuleCatalogService>();
        services.AddScoped<IRuleDefinitionService, RuleDefinitionService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<IRuleCatalogService>().Should().BeOfType<RuleCatalogService>();
        scope.ServiceProvider.GetRequiredService<IRuleDefinitionService>().Should().BeOfType<RuleDefinitionService>();
    }

    private const string ValidScopeJson = "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}";
    private const string ValidForbiddenApiParametersJson = "{\"forbiddenSymbols\":[\"System.Console.WriteLine\"],\"allowedAlternatives\":[\"ILogger.LogInformation\"],\"allowInTests\":false}";
    private const string ValidSymbolNamingParametersJson = "{\"symbolKinds\":[\"field\"],\"capitalization\":\"camelCase\",\"requiredPrefix\":\"_\",\"allowRegex\":\"\"}";
}

internal sealed class RuleDefinitionServiceFixture : IAsyncDisposable
{
    private RuleDefinitionServiceFixture(CodePassDbContext dbContext, IRuleDefinitionService service)
    {
        DbContext = dbContext;
        Service = service;
    }

    public CodePassDbContext DbContext { get; }

    public IRuleDefinitionService Service { get; }

    public static async Task<RuleDefinitionServiceFixture> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseInMemoryDatabase($"codepass-rule-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var catalog = new RuleCatalogService();
        var service = new RuleDefinitionService(dbContext, catalog);

        return new RuleDefinitionServiceFixture(dbContext, service);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }
}
