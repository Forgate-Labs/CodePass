using Bunit;
using CodePass.Web.Components.RuleAnalysis;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.RuleAnalysis;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class SolutionRuleSelectionPanelTests : TestContext
{
    [Fact]
    public async Task ToggleEnabledRule_ShouldCallSetRuleEnabledWithSelectedSolutionAndRuleId()
    {
        var solutionId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var selectionService = new RuleAnalysisTestSelectionService();
        selectionService.SelectionsBySolution[solutionId] =
        [
            RuleAnalysisComponentTestData.CreateSelection(
                code: "CP1000",
                title: "Avoid Console.WriteLine",
                severity: RuleSeverity.Warning,
                isGloballyEnabled: true,
                isEnabledForSolution: false,
                ruleId: ruleId)
        ];
        Services.AddSingleton<ISolutionRuleSelectionService>(selectionService);

        var cut = RenderComponent<SolutionRuleSelectionPanel>(parameters => parameters
            .Add(parameter => parameter.RegisteredSolutionId, solutionId)
            .Add(parameter => parameter.SolutionName, "Alpha"));

        cut.WaitForAssertion(() => cut.Find("[data-testid='rule-selection-switch']").HasAttribute("disabled").Should().BeFalse());

        await cut.Find("[data-testid='rule-selection-switch']").ChangeAsync(new ChangeEventArgs { Value = true });

        cut.WaitForAssertion(() =>
        {
            selectionService.SetRuleCalls.Should().ContainSingle();
            selectionService.SetRuleCalls[0].RegisteredSolutionId.Should().Be(solutionId);
            selectionService.SetRuleCalls[0].AuthoredRuleDefinitionId.Should().Be(ruleId);
            selectionService.SetRuleCalls[0].IsEnabled.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GloballyDisabledRules_ShouldRenderUnavailableAndNotToggle()
    {
        var solutionId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var selectionService = new RuleAnalysisTestSelectionService();
        selectionService.SelectionsBySolution[solutionId] =
        [
            RuleAnalysisComponentTestData.CreateSelection(
                code: "CP2000",
                title: "Disabled rule",
                severity: RuleSeverity.Error,
                isGloballyEnabled: false,
                isEnabledForSolution: false,
                ruleId: ruleId)
        ];
        Services.AddSingleton<ISolutionRuleSelectionService>(selectionService);

        var cut = RenderComponent<SolutionRuleSelectionPanel>(parameters => parameters
            .Add(parameter => parameter.RegisteredSolutionId, solutionId)
            .Add(parameter => parameter.SolutionName, "Alpha"));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rule-selection-unavailable']").TextContent.Should().Contain("Globally disabled");
            cut.Find("[data-testid='rule-selection-switch']").HasAttribute("disabled").Should().BeTrue();
        });

        await cut.Find("[data-testid='rule-selection-switch']").ChangeAsync(new ChangeEventArgs { Value = true });

        selectionService.SetRuleCalls.Should().BeEmpty();
    }
}
