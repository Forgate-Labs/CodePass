using System.Text.Json;
using Bunit;
using CodePass.Web.Components.Pages;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Rules;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class RuleDefinitionsPageTests : TestContext
{
    [Fact]
    public void EmptyState_ShouldRenderWhenNoAuthoredRulesExist()
    {
        var ruleService = new FakeRuleDefinitionService();
        Services.AddSingleton<IRuleDefinitionService>(ruleService);
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitions>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rules-empty-state']").TextContent.Should().Contain("No authored rules yet");
            cut.Markup.Should().Contain("never lists shipped production rule packs");
        });
    }

    [Fact]
    public void RulesList_ShouldRenderOnlyPersistedAuthoredRules()
    {
        var authoredRule = FakeRuleDefinitionService.CreateRule(
            code: "CP0001",
            title: "Avoid Console.WriteLine",
            kind: "forbidden_api_usage",
            severity: RuleSeverity.Warning);

        Services.AddSingleton<IRuleDefinitionService>(new FakeRuleDefinitionService(authoredRule));
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitions>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='rule-card-item']").Should().ContainSingle();
            cut.Markup.Should().Contain("Avoid Console.WriteLine");
            cut.Markup.Should().NotContain("Syntax presence policy");
            cut.Markup.Should().NotContain("Forbidden API usage");
            cut.Markup.Should().NotContain("Symbol naming policy");
        });
    }

    [Fact]
    public async Task SavingNewRule_ShouldRefreshTheVisibleAuthoredRulesList()
    {
        var ruleService = new FakeRuleDefinitionService();
        Services.AddSingleton<IRuleDefinitionService>(ruleService);
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitions>();

        await cut.Find("[data-testid='create-rule-button']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='rule-code-input']").ChangeAsync(new ChangeEventArgs { Value = "CP1000" });
        await cut.Find("[data-testid='rule-title-input']").ChangeAsync(new ChangeEventArgs { Value = "Avoid var" });
        await cut.Find("[data-testid='rule-kind-select']").ChangeAsync(new ChangeEventArgs { Value = "syntax_presence" });
        await cut.Find("[data-testid='select-field-mode']").ChangeAsync(new ChangeEventArgs { Value = "forbid" });
        await cut.Find("[data-testid='string-list-field-targets']").ChangeAsync(new ChangeEventArgs { Value = "local_declaration" });
        await cut.Find("[data-testid='string-list-field-syntaxKinds']").ChangeAsync(new ChangeEventArgs { Value = "var" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            ruleService.CreateCalls.Should().Be(1);
            ruleService.GetAllCalls.Should().BeGreaterThanOrEqualTo(2);
            cut.FindAll("[data-testid='rule-card-item']").Should().ContainSingle();
            cut.Markup.Should().Contain("CP1000");
            cut.Markup.Should().Contain("Avoid var");
        });
    }
}

internal sealed class FakeRuleDefinitionService(params AuthoredRuleDefinitionDto[] seededRules) : IRuleDefinitionService
{
    private readonly List<AuthoredRuleDefinitionDto> _rules = seededRules.OrderBy(rule => rule.Title).ThenBy(rule => rule.Code).ToList();

    public int GetAllCalls { get; private set; }
    public int CreateCalls { get; private set; }
    public int UpdateCalls { get; private set; }

    public List<SaveAuthoredRuleDefinitionRequest> CreatedRequests { get; } = [];
    public List<(Guid Id, SaveAuthoredRuleDefinitionRequest Request)> UpdatedRequests { get; } = [];

    public Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls++;
        return Task.FromResult<IReadOnlyList<AuthoredRuleDefinitionDto>>(_rules.OrderBy(rule => rule.Title).ThenBy(rule => rule.Code).ToList());
    }

    public Task<AuthoredRuleDefinitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<AuthoredRuleDefinitionDto?>(_rules.SingleOrDefault(rule => rule.Id == id));

    public Task<AuthoredRuleDefinitionDto> CreateAsync(SaveAuthoredRuleDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls++;
        CreatedRequests.Add(request);
        var created = CreateRule(request.Code, request.Title, request.RuleKind, request.Severity, request.Description, request.IsEnabled, request.ScopeJson, request.ParametersJson, request.SchemaVersion);
        _rules.Add(created);
        return Task.FromResult(created);
    }

    public Task<AuthoredRuleDefinitionDto> UpdateAsync(Guid id, SaveAuthoredRuleDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        UpdatedRequests.Add((id, request));

        var index = _rules.FindIndex(rule => rule.Id == id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Rule '{id}' not found.");
        }

        _rules[index] = CreateRule(request.Code, request.Title, request.RuleKind, request.Severity, request.Description, request.IsEnabled, request.ScopeJson, request.ParametersJson, request.SchemaVersion, id);
        return Task.FromResult(_rules[index]);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _rules.RemoveAll(rule => rule.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuthoredRuleDefinitionDto>>(_rules.Where(rule => rule.IsEnabled).ToList());

    public static AuthoredRuleDefinitionDto CreateRule(
        string code,
        string title,
        string kind,
        RuleSeverity severity,
        string? description = null,
        bool isEnabled = true,
        string? scopeJson = null,
        string? parametersJson = null,
        string schemaVersion = "1.0",
        Guid? id = null)
    {
        var now = DateTimeOffset.UtcNow;
        scopeJson ??= JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["projects"] = new[] { "*" },
            ["files"] = new[] { "**/*.cs" },
            ["excludeFiles"] = Array.Empty<string>()
        });
        parametersJson ??= JsonSerializer.Serialize(new Dictionary<string, object?>());

        return new AuthoredRuleDefinitionDto(
            id ?? Guid.NewGuid(),
            code,
            title,
            description,
            kind,
            schemaVersion,
            severity,
            scopeJson,
            parametersJson,
            "{}",
            isEnabled,
            now,
            now);
    }
}

internal sealed class FakeRuleCatalogService : IRuleCatalogService
{
    private static readonly IReadOnlyList<RuleKindCatalogEntry> RuleKinds =
    [
        new(
            "syntax_presence",
            "Syntax presence policy",
            "Require or forbid supported C# syntax constructs.",
            "1.0",
            "csharp",
            "syntax",
            [
                new RuleCatalogFieldDefinition("projects", "Projects", "Projects included by the rule.", "array", false, JsonSerializer.SerializeToElement(new[] { "*" })),
                new RuleCatalogFieldDefinition("files", "Files", "Files included by the rule.", "array", false, JsonSerializer.SerializeToElement(new[] { "**/*.cs" })),
                new RuleCatalogFieldDefinition("excludeFiles", "Exclude files", "Files excluded from the rule.", "array", false, JsonSerializer.SerializeToElement(Array.Empty<string>()))
            ],
            [
                new RuleCatalogFieldDefinition("mode", "Mode", "Whether matching syntax is forbidden or required.", "string", true, JsonSerializer.SerializeToElement("forbid"), new[] { "forbid", "require" }),
                new RuleCatalogFieldDefinition("targets", "Targets", "Closed target contexts for the syntax policy.", "array", true, JsonSerializer.SerializeToElement(new[] { "local_declaration" }), new[] { "local_declaration", "member_access" }),
                new RuleCatalogFieldDefinition("syntaxKinds", "Syntax kinds", "Supported syntax constructs to check.", "array", true, JsonSerializer.SerializeToElement(new[] { "var" }), new[] { "var", "goto" }),
                new RuleCatalogFieldDefinition("allowInTests", "Allow in tests", "Whether test projects can opt out.", "boolean", false, JsonSerializer.SerializeToElement(false))
            ]),
        new(
            "forbidden_api_usage",
            "Forbidden API usage",
            "Disallow explicit APIs and suggest safer alternatives.",
            "1.0",
            "csharp",
            "semantic",
            [],
            [
                new RuleCatalogFieldDefinition("forbiddenSymbols", "Forbidden symbols", "Symbols to block.", "array", true, JsonSerializer.SerializeToElement(new[] { "System.Console.WriteLine" }))
            ]),
        new(
            "symbol_naming",
            "Symbol naming policy",
            "Enforce naming conventions.",
            "1.0",
            "csharp",
            "semantic",
            [],
            [
                new RuleCatalogFieldDefinition("allowRegex", "Allow regex", "Regex that can explicitly pass validation.", "string", false, JsonSerializer.SerializeToElement(string.Empty))
            ])
    ];

    public Task<IReadOnlyList<RuleKindCatalogEntry>> GetRuleKindsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(RuleKinds);

    public Task<RuleKindCatalogEntry?> GetRuleKindAsync(string kind, CancellationToken cancellationToken = default)
        => Task.FromResult(RuleKinds.FirstOrDefault(ruleKind => string.Equals(ruleKind.Kind, kind, StringComparison.Ordinal)));
}
