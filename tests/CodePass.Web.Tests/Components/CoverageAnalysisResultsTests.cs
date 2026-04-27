using Bunit;
using CodePass.Web.Components.CoverageAnalysis;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.CoverageAnalysis;
using FluentAssertions;

namespace CodePass.Web.Tests.Components;

public sealed class CoverageAnalysisResultsTests : TestContext
{
    [Fact]
    public void NoResults_ShouldRenderEmptyState()
    {
        var cut = RenderComponent<CoverageAnalysisResults>(parameters => parameters
            .Add(parameter => parameter.Run, null));

        cut.Find("[data-testid='coverage-analysis-no-results']").TextContent.Should().Contain("No coverage-analysis runs yet");
    }

    [Fact]
    public void FailedRun_ShouldRenderReadableErrorMessage()
    {
        var run = CoverageAnalysisComponentTestData.CreateFailedRun(Guid.NewGuid(), "dotnet test failed with exit code 1.");

        var cut = RenderComponent<CoverageAnalysisResults>(parameters => parameters
            .Add(parameter => parameter.Run, run));

        cut.Find("[data-testid='coverage-run-status']").TextContent.Should().Contain("Failed");
        cut.Find("[data-testid='coverage-run-error-message']").TextContent.Should().Contain("dotnet test failed with exit code 1.");
    }

    [Fact]
    public void AggregateAndProjectSummary_ShouldRenderCoveragePercentages()
    {
        var run = CoverageAnalysisComponentTestData.CreateSucceededRun(Guid.NewGuid());

        var cut = RenderComponent<CoverageAnalysisResults>(parameters => parameters
            .Add(parameter => parameter.Run, run));

        cut.Find("[data-testid='coverage-run-status']").TextContent.Should().Contain("Succeeded");
        cut.Find("[data-testid='coverage-project-count']").TextContent.Should().Contain("1");
        cut.Find("[data-testid='coverage-class-count']").TextContent.Should().Contain("1");
        cut.Find("[data-testid='coverage-line-percent']").TextContent.Should().Contain("80.0%");
        cut.Find("[data-testid='coverage-branch-percent']").TextContent.Should().Contain("50.0%");

        var projectRow = cut.Find("[data-testid='coverage-project-row']");
        projectRow.TextContent.Should().Contain("CodePass.Web");
        projectRow.TextContent.Should().Contain("80/100");
        projectRow.TextContent.Should().Contain("80.0%");
        projectRow.TextContent.Should().Contain("5/10");
        projectRow.TextContent.Should().Contain("50.0%");
    }

    [Fact]
    public void ClassRows_ShouldRenderProjectClassFilePathAndPercentages()
    {
        var run = CoverageAnalysisComponentTestData.CreateSucceededRun(Guid.NewGuid());

        var cut = RenderComponent<CoverageAnalysisResults>(parameters => parameters
            .Add(parameter => parameter.Run, run));

        var classRow = cut.Find("[data-testid='coverage-class-row']");
        classRow.TextContent.Should().Contain("CodePass.Web");
        classRow.TextContent.Should().Contain("CoverageAnalysisResultService");
        classRow.TextContent.Should().Contain("src/Services/CoverageAnalysis/CoverageAnalysisResultService.cs");
        classRow.TextContent.Should().Contain("24/30");
        classRow.TextContent.Should().Contain("80.0%");
        classRow.TextContent.Should().Contain("2/4");
        classRow.TextContent.Should().Contain("50.0%");
    }

    [Fact]
    public void SucceededRunWithZeroClassRows_ShouldRenderSuccessStateWithoutCrashing()
    {
        var run = CoverageAnalysisComponentTestData.CreateSucceededRun(Guid.NewGuid(), classCoverages: []);

        var cut = RenderComponent<CoverageAnalysisResults>(parameters => parameters
            .Add(parameter => parameter.Run, run));

        cut.Find("[data-testid='coverage-zero-class-success']").TextContent.Should().Contain("completed successfully");
        cut.Find("[data-testid='coverage-no-class-rows']").TextContent.Should().Contain("No class-level coverage rows");
    }
}

internal static class CoverageAnalysisComponentTestData
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

    public static CoverageAnalysisRunDto CreateSucceededRun(
        Guid solutionId,
        string projectName = "CodePass.Web",
        string className = "CoverageAnalysisResultService",
        IReadOnlyList<CoverageProjectSummaryDto>? projectSummaries = null,
        IReadOnlyList<CoverageClassCoverageDto>? classCoverages = null)
    {
        var projects = projectSummaries ??
        [
            new CoverageProjectSummaryDto(
                Guid.NewGuid(),
                projectName,
                80,
                100,
                80.0,
                5,
                10,
                50.0)
        ];

        var classes = classCoverages ??
        [
            new CoverageClassCoverageDto(
                Guid.NewGuid(),
                projectName,
                className,
                "src/Services/CoverageAnalysis/CoverageAnalysisResultService.cs",
                24,
                30,
                80.0,
                2,
                4,
                50.0)
        ];

        return new CoverageAnalysisRunDto(
            Guid.NewGuid(),
            solutionId,
            CoverageAnalysisRunStatus.Succeeded,
            DateTimeOffset.UtcNow.AddMinutes(-2),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            projects.Count,
            classes.Count,
            80,
            100,
            80.0,
            5,
            10,
            50.0,
            null,
            projects,
            classes);
    }

    public static CoverageAnalysisRunDto CreateFailedRun(Guid solutionId, string errorMessage)
        => new(
            Guid.NewGuid(),
            solutionId,
            CoverageAnalysisRunStatus.Failed,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            errorMessage,
            [],
            []);
}
