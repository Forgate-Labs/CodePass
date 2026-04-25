using Bunit;
using CodePass.Web.Components.RuleAnalysis;
using CodePass.Web.Services.RuleAnalysis;
using FluentAssertions;

namespace CodePass.Web.Tests.Components;

public sealed class RuleAnalysisResultsTests : TestContext
{
    [Fact]
    public void FailedRun_ShouldRenderErrorMessage()
    {
        var run = RuleAnalysisComponentTestData.CreateFailedRun(Guid.NewGuid(), "The solution file could not be opened.");

        var cut = RenderComponent<RuleAnalysisResults>(parameters => parameters
            .Add(parameter => parameter.Run, run));

        cut.Find("[data-testid='run-status']").TextContent.Should().Contain("Failed");
        cut.Find("[data-testid='run-error-message']").TextContent.Should().Contain("The solution file could not be opened.");
    }

    [Fact]
    public void SucceededRunWithZeroViolations_ShouldRenderSuccessState()
    {
        var run = RuleAnalysisComponentTestData.CreateSucceededRun(Guid.NewGuid(), totalViolations: 0);

        var cut = RenderComponent<RuleAnalysisResults>(parameters => parameters
            .Add(parameter => parameter.Run, run));

        cut.Find("[data-testid='run-status']").TextContent.Should().Contain("Succeeded");
        cut.Find("[data-testid='zero-violations-success']").TextContent.Should().Contain("No violations found");
        cut.Find("[data-testid='run-total-violations']").TextContent.Should().Contain("0");
    }

    [Fact]
    public void ViolationRows_ShouldRenderSeverityFilePathAndLineColumn()
    {
        var violation = RuleAnalysisComponentTestData.CreateViolation(filePath: "src/App/Program.cs", startLine: 42, startColumn: 17, endLine: 42, endColumn: 30);
        var run = new RuleAnalysisRunDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CodePass.Web.Data.Entities.RuleAnalysisRunStatus.Succeeded,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            1,
            1,
            null,
            [
                new RuleAnalysisRuleGroupDto(
                    "CP1000",
                    "Avoid Console.WriteLine",
                    "forbidden_api_usage",
                    CodePass.Web.Data.Entities.RuleSeverity.Error,
                    1,
                    [violation])
            ]);

        var cut = RenderComponent<RuleAnalysisResults>(parameters => parameters
            .Add(parameter => parameter.Run, run));

        cut.Find("[data-testid='result-rule-code']").TextContent.Should().Contain("CP1000");
        cut.Find("[data-testid='result-rule-title']").TextContent.Should().Contain("Avoid Console.WriteLine");
        cut.Find("[data-testid='result-rule-kind']").TextContent.Should().Contain("forbidden_api_usage");
        cut.Find("[data-testid='violation-severity']").TextContent.Should().Contain("Error");
        cut.Find("[data-testid='violation-file-path']").TextContent.Should().Contain("src/App/Program.cs");
        cut.Find("[data-testid='violation-start-location']").TextContent.Should().Contain("42:17");
        cut.Find("[data-testid='violation-end-location']").TextContent.Should().Contain("42:30");
    }
}
