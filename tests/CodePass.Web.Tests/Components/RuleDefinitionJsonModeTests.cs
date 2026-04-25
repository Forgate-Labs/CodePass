using System.Text.Json;
using Bunit;
using CodePass.Web.Components.Rules;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Rules;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class RuleDefinitionJsonModeTests : TestContext
{
    [Fact]
    public async Task SwitchingToJsonMode_ShouldShowGeneratedFullDslDocument()
    {
        Services.AddSingleton<IRuleDefinitionService>(new FakeRuleDefinitionService());
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitionEditor>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true));

        await cut.Find("[data-testid='rule-code-input']").ChangeAsync(new ChangeEventArgs { Value = "CP3000" });
        await cut.Find("[data-testid='rule-title-input']").ChangeAsync(new ChangeEventArgs { Value = "Avoid var" });
        await cut.Find("[data-testid='rule-kind-select']").ChangeAsync(new ChangeEventArgs { Value = "syntax_presence" });
        await cut.Find("[data-testid='json-mode-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            var rawJson = cut.Find("[data-testid='rule-raw-json-input']").GetAttribute("value");
            rawJson.Should().Contain("\"id\": \"CP3000\"");
            rawJson.Should().Contain("\"title\": \"Avoid var\"");
            rawJson.Should().Contain("\"kind\": \"syntax_presence\"");
            rawJson.Should().Contain("\"scope\"");
            rawJson.Should().Contain("\"parameters\"");
        });
    }

    [Fact]
    public async Task InvalidRawJson_ShouldBlockSaveWithInlineError()
    {
        var ruleService = new FakeRuleDefinitionService();
        Services.AddSingleton<IRuleDefinitionService>(ruleService);
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitionEditor>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true));

        await cut.Find("[data-testid='rule-code-input']").ChangeAsync(new ChangeEventArgs { Value = "CP3001" });
        await cut.Find("[data-testid='rule-title-input']").ChangeAsync(new ChangeEventArgs { Value = "Avoid var" });
        await cut.Find("[data-testid='rule-kind-select']").ChangeAsync(new ChangeEventArgs { Value = "syntax_presence" });
        await cut.Find("[data-testid='json-mode-button']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='rule-raw-json-input']").ChangeAsync(new ChangeEventArgs { Value = "{ invalid json" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            ruleService.CreateCalls.Should().Be(0);
            cut.Find("[data-testid='rule-editor-json-error']").TextContent.Should().Contain("valid JSON");
        });
    }

    [Fact]
    public async Task SwitchingBackToSchemaMode_ShouldRehydrateEditedJsonValues()
    {
        Services.AddSingleton<IRuleDefinitionService>(new FakeRuleDefinitionService());
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitionEditor>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true));

        await cut.Find("[data-testid='rule-code-input']").ChangeAsync(new ChangeEventArgs { Value = "CP3002" });
        await cut.Find("[data-testid='rule-title-input']").ChangeAsync(new ChangeEventArgs { Value = "Avoid var" });
        await cut.Find("[data-testid='rule-kind-select']").ChangeAsync(new ChangeEventArgs { Value = "syntax_presence" });
        await cut.Find("[data-testid='json-mode-button']").ClickAsync(new MouseEventArgs());

        var editedJson = JsonSerializer.Serialize(new
        {
            id = "CP3002",
            title = "Avoid goto",
            description = "Updated through raw json",
            kind = "syntax_presence",
            schemaVersion = "1.0",
            severity = "error",
            enabled = true,
            language = "csharp",
            scope = new
            {
                projects = new[] { "*" },
                files = new[] { "**/*.cs" },
                excludeFiles = Array.Empty<string>()
            },
            parameters = new
            {
                mode = "forbid",
                targets = new[] { "member_access" },
                syntaxKinds = new[] { "goto" },
                allowInTests = false
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        await cut.Find("[data-testid='rule-raw-json-input']").ChangeAsync(new ChangeEventArgs { Value = editedJson });
        await cut.Find("[data-testid='schema-mode-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rule-title-input']").GetAttribute("value").Should().Be("Avoid goto");
            cut.Find("[data-testid='rule-severity-input']").GetAttribute("value").Should().Be(nameof(RuleSeverity.Error));
            cut.Find("[data-testid='string-list-field-targets']").GetAttribute("value").Should().Contain("member_access");
            cut.Find("[data-testid='string-list-field-syntaxKinds']").GetAttribute("value").Should().Contain("goto");
        });
    }
}
