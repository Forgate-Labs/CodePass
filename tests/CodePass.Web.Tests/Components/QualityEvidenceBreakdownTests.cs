using Bunit;
using CodePass.Web.Components.Dashboard;
using CodePass.Web.Services.Dashboard;
using FluentAssertions;

namespace CodePass.Web.Tests.Components;

public sealed class QualityEvidenceBreakdownTests : TestContext
{
    [Fact]
    public void MissingSnapshot_ShouldRenderCompactEmptyState()
    {
        var cut = RenderComponent<QualityEvidenceBreakdown>(parameters => parameters
            .Add(parameter => parameter.Snapshot, null));

        cut.Find("[data-testid='quality-evidence-empty-state']").TextContent.Should().Contain("Run rule analysis and coverage analysis");
        cut.FindAll("[data-testid='rule-contribution-card']").Should().BeEmpty();
        cut.FindAll("[data-testid='coverage-contribution-card']").Should().BeEmpty();
    }

    [Fact]
    public void Snapshot_ShouldRenderRuleContributionDetailsAndLink()
    {
        var snapshot = CreateSnapshot();

        var cut = RenderComponent<QualityEvidenceBreakdown>(parameters => parameters
            .Add(parameter => parameter.Snapshot, snapshot));

        var ruleCard = cut.Find("[data-testid='rule-contribution-card']");
        ruleCard.TextContent.Should().Contain("Rule analysis contribution");
        ruleCard.TextContent.Should().Contain("Open rule analysis");
        ruleCard.QuerySelector("a")!.GetAttribute("href").Should().Be("/analysis/rules");

        cut.Find("[data-testid='rule-contribution-points']").TextContent.Should().Be("70/100");
        cut.Find("[data-testid='rule-total-violations']").TextContent.Should().Be("6");
        cut.Find("[data-testid='rule-error-count']").TextContent.Should().Be("1");
        cut.Find("[data-testid='rule-warning-count']").TextContent.Should().Be("2");
        cut.Find("[data-testid='rule-info-count']").TextContent.Should().Be("3");
        cut.Find("[data-testid='rule-latest-status']").TextContent.Should().Be("Succeeded");
        cut.Find("[data-testid='rule-contribution-summary']").TextContent.Should().Contain("Rule analysis found 6 violations.");
    }

    [Fact]
    public void Snapshot_ShouldRenderCoverageContributionDetailsAndLink()
    {
        var snapshot = CreateSnapshot();

        var cut = RenderComponent<QualityEvidenceBreakdown>(parameters => parameters
            .Add(parameter => parameter.Snapshot, snapshot));

        var coverageCard = cut.Find("[data-testid='coverage-contribution-card']");
        coverageCard.TextContent.Should().Contain("Coverage contribution");
        coverageCard.TextContent.Should().Contain("Open coverage analysis");
        coverageCard.QuerySelector("a")!.GetAttribute("href").Should().Be("/analysis/coverage");

        cut.Find("[data-testid='coverage-contribution-points']").TextContent.Should().Be("83/100");
        cut.Find("[data-testid='coverage-line-percent']").TextContent.Should().Be("83.0%");
        cut.Find("[data-testid='coverage-line-counts']").TextContent.Should().Be("83/100");
        cut.Find("[data-testid='coverage-latest-status']").TextContent.Should().Be("Succeeded");
        cut.Find("[data-testid='coverage-contribution-summary']").TextContent.Should().Contain("Line coverage is 83.0%.");
    }

    [Fact]
    public void FailedOrMissingEvidence_ShouldRenderReadableReasonsWithoutCrashing()
    {
        var snapshot = CreateSnapshot(
            new QualityRuleContributionDto(
                100,
                0,
                QualityEvidenceStatus.Failed,
                0,
                0,
                0,
                0,
                "Rule analysis failed before producing violations.",
                ["Rule analysis failed: project could not be loaded."]),
            new QualityCoverageContributionDto(
                100,
                0,
                QualityEvidenceStatus.Missing,
                null,
                null,
                null,
                "Coverage analysis has not run yet.",
                ["Coverage evidence is missing."]));

        var cut = RenderComponent<QualityEvidenceBreakdown>(parameters => parameters
            .Add(parameter => parameter.Snapshot, snapshot));

        cut.Find("[data-testid='rule-evidence-status']").TextContent.Should().Be("Failed");
        cut.Find("[data-testid='rule-contribution-reasons']").TextContent.Should().Contain("project could not be loaded");

        cut.Find("[data-testid='coverage-evidence-status']").TextContent.Should().Be("Missing");
        cut.Find("[data-testid='coverage-line-percent']").TextContent.Should().Be("Not available");
        cut.Find("[data-testid='coverage-line-counts']").TextContent.Should().Be("Not available");
        cut.Find("[data-testid='coverage-contribution-reasons']").TextContent.Should().Contain("Coverage evidence is missing.");
    }

    private static QualityScoreSnapshotDto CreateSnapshot(
        QualityRuleContributionDto? ruleContribution = null,
        QualityCoverageContributionDto? coverageContribution = null)
    {
        return new QualityScoreSnapshotDto(
            Guid.NewGuid(),
            76.5,
            QualityScoreStatus.Fail,
            ruleContribution ?? new QualityRuleContributionDto(
                100,
                70,
                QualityEvidenceStatus.Succeeded,
                1,
                2,
                3,
                6,
                "Rule analysis found 6 violations.",
                []),
            coverageContribution ?? new QualityCoverageContributionDto(
                100,
                83,
                QualityEvidenceStatus.Succeeded,
                83,
                83,
                100,
                "Line coverage is 83.0%.",
                []),
            []);
    }
}
