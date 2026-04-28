using Bunit;
using CodePass.Web.Components.Dashboard;
using CodePass.Web.Services.Dashboard;
using FluentAssertions;

namespace CodePass.Web.Tests.Components;

public sealed class QualityScoreSummaryTests : TestContext
{
    [Fact]
    public void MissingSnapshot_ShouldRenderExplicitEmptyState()
    {
        var cut = RenderComponent<QualityScoreSummary>(parameters => parameters
            .Add(parameter => parameter.Snapshot, null)
            .Add(parameter => parameter.SolutionName, "CodePass"));

        cut.Find("[data-testid='quality-score-empty-state']").TextContent.Should().Contain("No current quality score");
        cut.Find("[data-testid='quality-score-empty-state']").TextContent.Should().Contain("rule analysis and coverage analysis");
        cut.Markup.Should().Contain("CodePass");
        cut.FindAll("[data-testid='quality-score-value']").Should().BeEmpty();
        cut.FindAll("[data-testid='quality-status-badge']").Should().BeEmpty();
    }

    [Fact]
    public void PassingSnapshot_ShouldRenderScoreSuffixAndSuccessBadge()
    {
        var snapshot = CreateSnapshot(QualityScoreStatus.Pass, score: 92.5);

        var cut = RenderComponent<QualityScoreSummary>(parameters => parameters
            .Add(parameter => parameter.Snapshot, snapshot)
            .Add(parameter => parameter.SolutionName, "CodePass.Web"));

        cut.Find("[data-testid='quality-score-value']").TextContent.Should().Be("92.5");
        cut.Markup.Should().Contain("/100");
        cut.Markup.Should().Contain("CodePass.Web");

        var badge = cut.Find("[data-testid='quality-status-badge']");
        badge.TextContent.Should().Be("Pass");
        badge.ClassList.Should().Contain("text-bg-success");
    }

    [Fact]
    public void FailingSnapshot_ShouldRenderDangerBadgeAndBlockingReasons()
    {
        var snapshot = CreateSnapshot(
            QualityScoreStatus.Fail,
            score: 45,
            blockingReasons:
            [
                "Latest rule-analysis evidence has failed.",
                "Latest coverage evidence is missing."
            ]);

        var cut = RenderComponent<QualityScoreSummary>(parameters => parameters
            .Add(parameter => parameter.Snapshot, snapshot)
            .Add(parameter => parameter.SolutionName, null));

        cut.Find("[data-testid='quality-score-value']").TextContent.Should().Be("45");
        cut.Markup.Should().Contain("Selected solution");

        var badge = cut.Find("[data-testid='quality-status-badge']");
        badge.TextContent.Should().Be("Fail");
        badge.ClassList.Should().Contain("text-bg-danger");

        var reasons = cut.Find("[data-testid='quality-snapshot-reasons']");
        reasons.TextContent.Should().Contain("Latest rule-analysis evidence has failed.");
        reasons.TextContent.Should().Contain("Latest coverage evidence is missing.");
    }

    private static QualityScoreSnapshotDto CreateSnapshot(
        QualityScoreStatus status,
        double score,
        IReadOnlyList<string>? blockingReasons = null)
    {
        return new QualityScoreSnapshotDto(
            Guid.NewGuid(),
            score,
            status,
            new QualityRuleContributionDto(
                50,
                status == QualityScoreStatus.Pass ? 50 : 10,
                QualityEvidenceStatus.Succeeded,
                status == QualityScoreStatus.Pass ? 0 : 2,
                status == QualityScoreStatus.Pass ? 0 : 1,
                0,
                status == QualityScoreStatus.Pass ? 0 : 3,
                "Rule evidence summary",
                []),
            new QualityCoverageContributionDto(
                50,
                status == QualityScoreStatus.Pass ? 42.5 : 35,
                QualityEvidenceStatus.Succeeded,
                85,
                85,
                100,
                "Coverage evidence summary",
                []),
            blockingReasons ?? []);
    }
}
