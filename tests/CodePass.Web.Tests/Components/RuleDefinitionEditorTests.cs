using Bunit;
using CodePass.Web.Components.Rules;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Rules;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class RuleDefinitionEditorTests : TestContext
{
    [Fact]
    public async Task SelectingRuleKind_ShouldRenderGeneratedSchemaFieldsFromCatalogMetadata()
    {
        Services.AddSingleton<IRuleDefinitionService>(new FakeRuleDefinitionService());
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitionEditor>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true));

        await cut.Find("[data-testid='rule-kind-select']").ChangeAsync(new ChangeEventArgs { Value = "syntax_presence" });

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='dynamic-schema-section']").TextContent.Should().Contain("Syntax presence policy");
            cut.Find("[data-testid='select-field-mode']").GetAttribute("value").Should().Be("forbid");
            cut.Find("[data-testid='multi-select-field-targets'] option[value='local_declaration']").HasAttribute("selected").Should().BeTrue();
            cut.Find("[data-testid='multi-select-field-syntaxKinds'] option[value='var']").HasAttribute("selected").Should().BeTrue();
            cut.Find("[data-testid='checkbox-field-allowInTests']").HasAttribute("checked").Should().BeFalse();
        });
    }

    [Fact]
    public async Task SelectingRuleKind_ShouldUseScrollableBootstrapModalStructureWithReachableFooter()
    {
        Services.AddSingleton<IRuleDefinitionService>(new FakeRuleDefinitionService());
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitionEditor>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true));

        await cut.Find("[data-testid='rule-kind-select']").ChangeAsync(new ChangeEventArgs { Value = "syntax_presence" });
        await cut.Find("[data-testid='json-mode-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            var dialog = cut.Find(".modal-dialog");
            dialog.ClassList.Should().Contain("modal-dialog-scrollable");

            var form = cut.Find("form.rule-definition-editor-form.modal-content");
            form.ParentElement?.ClassList.Contains("modal-dialog").Should().BeTrue();
            form.Children.Any(child => child.ClassList.Contains("modal-body")).Should().BeTrue();
            form.Children.Any(child => child.ClassList.Contains("modal-footer")).Should().BeTrue();
            form.QuerySelector(".modal-body .modal-footer").Should().BeNull();
            cut.Find("[data-testid='save-rule-button']").TextContent.Should().Contain("Save authored rule");
        });
    }

    [Fact]
    public async Task MissingRequiredMetadata_ShouldBlockSaveBeforeServiceCall()
    {
        var ruleService = new FakeRuleDefinitionService();
        Services.AddSingleton<IRuleDefinitionService>(ruleService);
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var cut = RenderComponent<RuleDefinitionEditor>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true));

        await cut.Find("[data-testid='rule-kind-select']").ChangeAsync(new ChangeEventArgs { Value = "syntax_presence" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Rule code is required.");
            cut.Markup.Should().Contain("Title is required.");
            ruleService.CreateCalls.Should().Be(0);
        });
    }

    [Fact]
    public async Task ValidSubmit_ShouldCreateRuleAndRaiseSavedCallback()
    {
        var ruleService = new FakeRuleDefinitionService();
        Services.AddSingleton<IRuleDefinitionService>(ruleService);
        Services.AddSingleton<IRuleCatalogService>(new FakeRuleCatalogService());

        var saveCount = 0;
        var cut = RenderComponent<RuleDefinitionEditor>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true)
            .Add(parameter => parameter.OnSaved, EventCallback.Factory.Create(this, () => saveCount++)));

        await cut.Find("[data-testid='rule-code-input']").ChangeAsync(new ChangeEventArgs { Value = "CP2000" });
        await cut.Find("[data-testid='rule-title-input']").ChangeAsync(new ChangeEventArgs { Value = "Prefer member access" });
        await cut.Find("[data-testid='rule-kind-select']").ChangeAsync(new ChangeEventArgs { Value = "syntax_presence" });
        await cut.Find("[data-testid='select-field-mode']").ChangeAsync(new ChangeEventArgs { Value = "require" });
        await cut.Find("[data-testid='multi-select-field-targets']").ChangeAsync(new ChangeEventArgs { Value = new[] { "member_access" } });
        await cut.Find("[data-testid='multi-select-field-syntaxKinds']").ChangeAsync(new ChangeEventArgs { Value = new[] { "goto" } });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            saveCount.Should().Be(1);
            ruleService.CreatedRequests.Should().ContainSingle();
            ruleService.CreatedRequests[0].Code.Should().Be("CP2000");
            ruleService.CreatedRequests[0].Title.Should().Be("Prefer member access");
            ruleService.CreatedRequests[0].RuleKind.Should().Be("syntax_presence");
            ruleService.CreatedRequests[0].Severity.Should().Be(RuleSeverity.Warning);
            ruleService.CreatedRequests[0].ScopeJson.Should().Contain("projects");
            ruleService.CreatedRequests[0].ParametersJson.Should().Contain("member_access");
            ruleService.CreatedRequests[0].ParametersJson.Should().Contain("goto");
        });
    }
}
