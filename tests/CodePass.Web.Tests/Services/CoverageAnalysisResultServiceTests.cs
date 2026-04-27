using System.Data.Common;
using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.CoverageAnalysis;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CoverageAnalysisResultModel = CodePass.Web.Services.CoverageAnalysis.CoverageAnalysisResult;
using CoverageClassCoverageModel = CodePass.Web.Services.CoverageAnalysis.CoverageClassCoverage;
using CoverageProjectSummaryModel = CodePass.Web.Services.CoverageAnalysis.CoverageProjectSummary;

namespace CodePass.Web.Tests.Services;

public sealed class CoverageAnalysisResultServiceTests
{
    [Fact]
    public async Task CreateRunningRunAsync_ShouldPersistRunningRunForExistingRegisteredSolution()
    {
        await using var fixture = await CoverageAnalysisResultServiceFixture.CreateAsync();

        var run = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id);
        var persistedRun = await fixture.Service.GetRunAsync(run.Id);

        run.RegisteredSolutionId.Should().Be(fixture.SolutionA.Id);
        run.Status.Should().Be(CoverageAnalysisRunStatus.Running);
        run.CompletedAtUtc.Should().BeNull();
        run.ProjectCount.Should().Be(0);
        run.ClassCount.Should().Be(0);
        run.ErrorMessage.Should().BeNull();
        run.ProjectSummaries.Should().BeEmpty();
        run.ClassCoverages.Should().BeEmpty();
        persistedRun.Should().NotBeNull();
        persistedRun!.Status.Should().Be(CoverageAnalysisRunStatus.Running);
    }

    [Fact]
    public async Task CreateRunningRunAsync_ShouldThrowClearlyForUnknownRegisteredSolution()
    {
        await using var fixture = await CoverageAnalysisResultServiceFixture.CreateAsync();
        var unknownSolutionId = Guid.NewGuid();

        var action = async () => await fixture.Service.CreateRunningRunAsync(unknownSolutionId);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Registered solution '{unknownSolutionId}' was not found.*");
    }

    [Fact]
    public async Task MarkSucceededAsync_ShouldPersistAggregateCountsAndOrderedChildRows()
    {
        await using var fixture = await CoverageAnalysisResultServiceFixture.CreateAsync();
        var run = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id);

        var completedRun = await fixture.Service.MarkSucceededAsync(run.Id, CreateCoverageResult());
        var persistedRun = await fixture.Service.GetRunAsync(run.Id);

        completedRun.Status.Should().Be(CoverageAnalysisRunStatus.Succeeded);
        completedRun.CompletedAtUtc.Should().NotBeNull();
        completedRun.ErrorMessage.Should().BeNull();
        completedRun.ProjectCount.Should().Be(2);
        completedRun.ClassCount.Should().Be(3);
        completedRun.CoveredLineCount.Should().Be(35);
        completedRun.TotalLineCount.Should().Be(50);
        completedRun.LineCoveragePercent.Should().Be(70);
        completedRun.CoveredBranchCount.Should().Be(9);
        completedRun.TotalBranchCount.Should().Be(12);
        completedRun.BranchCoveragePercent.Should().Be(75);

        completedRun.ProjectSummaries.Select(summary => summary.ProjectName)
            .Should().Equal("Alpha.Project", "Beta.Project");
        completedRun.ProjectSummaries[0].CoveredLineCount.Should().Be(15);
        completedRun.ProjectSummaries[0].TotalLineCount.Should().Be(20);
        completedRun.ProjectSummaries[0].LineCoveragePercent.Should().Be(75);
        completedRun.ProjectSummaries[0].CoveredBranchCount.Should().Be(5);
        completedRun.ProjectSummaries[0].TotalBranchCount.Should().Be(6);
        completedRun.ProjectSummaries[0].BranchCoveragePercent.Should().Be(83.33);

        completedRun.ClassCoverages.Select(classCoverage =>
                $"{classCoverage.ProjectName}|{classCoverage.ClassName}|{classCoverage.FilePath}")
            .Should().Equal(
                "Alpha.Project|Alpha.Project.FirstClass|src/FirstClass.cs",
                "Alpha.Project|Alpha.Project.SecondClass|src/SecondClass.cs",
                "Beta.Project|Beta.Project.ZetaClass|src/ZetaClass.cs");
        completedRun.ClassCoverages[0].CoveredLineCount.Should().Be(10);
        completedRun.ClassCoverages[0].TotalLineCount.Should().Be(10);
        completedRun.ClassCoverages[0].LineCoveragePercent.Should().Be(100);
        completedRun.ClassCoverages[0].CoveredBranchCount.Should().Be(4);
        completedRun.ClassCoverages[0].TotalBranchCount.Should().Be(4);
        completedRun.ClassCoverages[0].BranchCoveragePercent.Should().Be(100);

        persistedRun.Should().NotBeNull();
        persistedRun!.ProjectSummaries.Select(summary => summary.ProjectName)
            .Should().Equal("Alpha.Project", "Beta.Project");
        persistedRun.ClassCoverages.Select(classCoverage => classCoverage.ClassName)
            .Should().Equal("Alpha.Project.FirstClass", "Alpha.Project.SecondClass", "Beta.Project.ZetaClass");
    }

    [Fact]
    public async Task MarkSucceededAsync_ShouldReplaceExistingChildRows()
    {
        await using var fixture = await CoverageAnalysisResultServiceFixture.CreateAsync();
        var run = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id);

        await fixture.Service.MarkSucceededAsync(run.Id, CreateCoverageResult());
        var replacedRun = await fixture.Service.MarkSucceededAsync(run.Id, new CoverageAnalysisResultModel(
            [new CoverageProjectSummaryModel("Replacement.Project", 1, 2, 50, 0, 0, 0)],
            [new CoverageClassCoverageModel("Replacement.Project", "Replacement.Class", "src/Replacement.cs", 1, 2, 50, 0, 0, 0)]));

        replacedRun.ProjectCount.Should().Be(1);
        replacedRun.ClassCount.Should().Be(1);
        replacedRun.ProjectSummaries.Select(summary => summary.ProjectName).Should().Equal("Replacement.Project");
        replacedRun.ClassCoverages.Select(classCoverage => classCoverage.ClassName).Should().Equal("Replacement.Class");
        (await fixture.DbContext.CoverageProjectSummaries.CountAsync()).Should().Be(1);
        (await fixture.DbContext.CoverageClassCoverages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task MarkFailedAsync_ShouldPreserveErrorMessageAndNoChildRows()
    {
        await using var fixture = await CoverageAnalysisResultServiceFixture.CreateAsync();
        var run = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id);
        await fixture.Service.MarkSucceededAsync(run.Id, CreateCoverageResult());

        var failedRun = await fixture.Service.MarkFailedAsync(run.Id, "dotnet test failed with exit code 1.");
        var persistedRun = await fixture.Service.GetRunAsync(run.Id);

        failedRun.Status.Should().Be(CoverageAnalysisRunStatus.Failed);
        failedRun.CompletedAtUtc.Should().NotBeNull();
        failedRun.ErrorMessage.Should().Be("dotnet test failed with exit code 1.");
        failedRun.ProjectCount.Should().Be(0);
        failedRun.ClassCount.Should().Be(0);
        failedRun.ProjectSummaries.Should().BeEmpty();
        failedRun.ClassCoverages.Should().BeEmpty();
        persistedRun.Should().NotBeNull();
        persistedRun!.Status.Should().Be(CoverageAnalysisRunStatus.Failed);
        persistedRun.ErrorMessage.Should().Be("dotnet test failed with exit code 1.");
        persistedRun.ProjectSummaries.Should().BeEmpty();
        persistedRun.ClassCoverages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestRunForSolutionAsync_ShouldReturnNewestRunOnlyForRequestedSolution()
    {
        await using var fixture = await CoverageAnalysisResultServiceFixture.CreateAsync();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        var olderSolutionARun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id);
        await SetStartedAtAsync(fixture.DbContext, olderSolutionARun.Id, baseTime);
        await fixture.Service.MarkSucceededAsync(olderSolutionARun.Id, CreateCoverageResult());

        var solutionBRun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionB.Id);
        await SetStartedAtAsync(fixture.DbContext, solutionBRun.Id, baseTime.AddMinutes(1));
        await fixture.Service.MarkFailedAsync(solutionBRun.Id, "No test projects found.");

        var newestSolutionARun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id);
        await SetStartedAtAsync(fixture.DbContext, newestSolutionARun.Id, baseTime.AddMinutes(2));
        await fixture.Service.MarkSucceededAsync(newestSolutionARun.Id, new CoverageAnalysisResultModel([], []));

        var latestSolutionA = await fixture.Service.GetLatestRunForSolutionAsync(fixture.SolutionA.Id);
        var latestSolutionB = await fixture.Service.GetLatestRunForSolutionAsync(fixture.SolutionB.Id);

        latestSolutionA.Should().NotBeNull();
        latestSolutionA!.Id.Should().Be(newestSolutionARun.Id);
        latestSolutionA.RegisteredSolutionId.Should().Be(fixture.SolutionA.Id);
        latestSolutionA.ProjectCount.Should().Be(0);
        latestSolutionB.Should().NotBeNull();
        latestSolutionB!.Id.Should().Be(solutionBRun.Id);
        latestSolutionB.RegisteredSolutionId.Should().Be(fixture.SolutionB.Id);
        latestSolutionB.Status.Should().Be(CoverageAnalysisRunStatus.Failed);
    }

    [Fact]
    public async Task GetLatestRunForSolutionAsync_ShouldOrderRunsWithSqliteProvider()
    {
        await using var fixture = await CoverageAnalysisResultServiceFixture.CreateSqliteAsync();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        var olderRun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id);
        await SetStartedAtAsync(fixture.DbContext, olderRun.Id, baseTime);
        await fixture.Service.MarkSucceededAsync(olderRun.Id, CreateCoverageResult());

        var newestRun = await fixture.Service.CreateRunningRunAsync(fixture.SolutionA.Id);
        await SetStartedAtAsync(fixture.DbContext, newestRun.Id, baseTime.AddMinutes(5));
        await fixture.Service.MarkSucceededAsync(newestRun.Id, CreateCoverageResult());

        var latestRun = await fixture.Service.GetLatestRunForSolutionAsync(fixture.SolutionA.Id);

        latestRun.Should().NotBeNull();
        latestRun!.Id.Should().Be(newestRun.Id);
        latestRun.ProjectSummaries.Should().HaveCount(2);
        latestRun.ClassCoverages.Should().HaveCount(3);
    }

    private static CoverageAnalysisResultModel CreateCoverageResult() => new(
        [
            new CoverageProjectSummaryModel("Beta.Project", 20, 30, 66.67, 4, 6, 66.67),
            new CoverageProjectSummaryModel("Alpha.Project", 15, 20, 75, 5, 6, 83.33)
        ],
        [
            new CoverageClassCoverageModel("Beta.Project", "Beta.Project.ZetaClass", "src/ZetaClass.cs", 20, 30, 66.67, 4, 6, 66.67),
            new CoverageClassCoverageModel("Alpha.Project", "Alpha.Project.SecondClass", "src/SecondClass.cs", 5, 10, 50, 1, 2, 50),
            new CoverageClassCoverageModel("Alpha.Project", "Alpha.Project.FirstClass", "src/FirstClass.cs", 10, 10, 100, 4, 4, 100)
        ]);

    private static async Task SetStartedAtAsync(CodePassDbContext dbContext, Guid runId, DateTimeOffset startedAtUtc)
    {
        var run = await dbContext.CoverageAnalysisRuns.SingleAsync(existing => existing.Id == runId);
        run.StartedAtUtc = startedAtUtc;
        await dbContext.SaveChangesAsync();
    }
}

internal sealed class CoverageAnalysisResultServiceFixture : IAsyncDisposable
{
    private CoverageAnalysisResultServiceFixture(
        CodePassDbContext dbContext,
        ICoverageAnalysisResultService service,
        RegisteredSolution solutionA,
        RegisteredSolution solutionB,
        DbConnection? dbConnection = null)
    {
        DbContext = dbContext;
        Service = service;
        SolutionA = solutionA;
        SolutionB = solutionB;
        DbConnection = dbConnection;
    }

    public CodePassDbContext DbContext { get; }

    public ICoverageAnalysisResultService Service { get; }

    public RegisteredSolution SolutionA { get; }

    public RegisteredSolution SolutionB { get; }

    private DbConnection? DbConnection { get; }

    public static async Task<CoverageAnalysisResultServiceFixture> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseInMemoryDatabase($"codepass-coverage-analysis-results-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var solutionA = CreateSolution("Solution A", "/tmp/coverage-solution-a.sln", now);
        var solutionB = CreateSolution("Solution B", "/tmp/coverage-solution-b.sln", now);

        dbContext.RegisteredSolutions.AddRange(solutionA, solutionB);
        await dbContext.SaveChangesAsync();

        return new CoverageAnalysisResultServiceFixture(
            dbContext,
            new CoverageAnalysisResultService(dbContext),
            solutionA,
            solutionB);
    }

    public static async Task<CoverageAnalysisResultServiceFixture> CreateSqliteAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var solutionA = CreateSolution("Solution A", "/tmp/coverage-solution-a.sln", now);
        var solutionB = CreateSolution("Solution B", "/tmp/coverage-solution-b.sln", now);

        dbContext.RegisteredSolutions.AddRange(solutionA, solutionB);
        await dbContext.SaveChangesAsync();

        return new CoverageAnalysisResultServiceFixture(
            dbContext,
            new CoverageAnalysisResultService(dbContext),
            solutionA,
            solutionB,
            connection);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();

        if (DbConnection is not null)
        {
            await DbConnection.DisposeAsync();
        }
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
}
