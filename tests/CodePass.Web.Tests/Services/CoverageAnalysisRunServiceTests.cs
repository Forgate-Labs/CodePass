using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoverageAnalysisResultModel = CodePass.Web.Services.CoverageAnalysis.CoverageAnalysisResult;
using CoverageClassCoverageModel = CodePass.Web.Services.CoverageAnalysis.CoverageClassCoverage;
using CoverageProjectSummaryModel = CodePass.Web.Services.CoverageAnalysis.CoverageProjectSummary;

namespace CodePass.Web.Tests.Services;

public sealed class CoverageAnalysisRunServiceTests
{
    [Fact]
    public async Task StartRunAsync_ShouldAnalyzeValidSolutionPathAndPersistNormalizedResults()
    {
        await using var fixture = await CoverageAnalysisRunServiceFixture.CreateAsync();
        fixture.Analyzer.ResultToReturn = CreateCoverageResult();

        var run = await fixture.Service.StartRunAsync(fixture.ValidSolution.Id);

        run.Status.Should().Be(CoverageAnalysisRunStatus.Succeeded);
        run.RegisteredSolutionId.Should().Be(fixture.ValidSolution.Id);
        run.ProjectCount.Should().Be(1);
        run.ClassCount.Should().Be(2);
        run.CoveredLineCount.Should().Be(15);
        run.TotalLineCount.Should().Be(20);
        run.LineCoveragePercent.Should().Be(75);
        run.CoveredBranchCount.Should().Be(5);
        run.TotalBranchCount.Should().Be(6);
        run.BranchCoveragePercent.Should().Be(83.33);
        run.ProjectSummaries.Select(summary => summary.ProjectName).Should().Equal("Alpha.Project");
        run.ClassCoverages.Select(classCoverage => classCoverage.ClassName)
            .Should().Equal("Alpha.Project.FirstClass", "Alpha.Project.SecondClass");

        var call = fixture.Analyzer.Calls.Should().ContainSingle().Subject;
        call.SolutionPath.Should().Be(fixture.ValidSolution.SolutionPath);

        var persistedRun = await fixture.DbContext.CoverageAnalysisRuns
            .Include(existing => existing.ProjectSummaries)
            .Include(existing => existing.ClassCoverages)
            .SingleAsync(existing => existing.Id == run.Id);
        persistedRun.Status.Should().Be(CoverageAnalysisRunStatus.Succeeded);
        persistedRun.ProjectSummaries.Should().ContainSingle(summary => summary.ProjectName == "Alpha.Project");
        persistedRun.ClassCoverages.Should().HaveCount(2);
    }

    [Fact]
    public async Task StartRunAsync_ShouldCreateFailedRunForNonValidSolutionWithoutCallingAnalyzer()
    {
        await using var fixture = await CoverageAnalysisRunServiceFixture.CreateAsync();

        var run = await fixture.Service.StartRunAsync(fixture.InvalidSolution.Id);

        run.Status.Should().Be(CoverageAnalysisRunStatus.Failed);
        run.RegisteredSolutionId.Should().Be(fixture.InvalidSolution.Id);
        run.ErrorMessage.Should().Contain("Only valid registered solutions can be analyzed for coverage");
        run.ErrorMessage.Should().Contain(RegisteredSolutionStatus.FileNotFound.ToString());
        run.ProjectSummaries.Should().BeEmpty();
        run.ClassCoverages.Should().BeEmpty();
        fixture.Analyzer.Calls.Should().BeEmpty();

        var persistedRun = await fixture.DbContext.CoverageAnalysisRuns.SingleAsync(existing => existing.Id == run.Id);
        persistedRun.Status.Should().Be(CoverageAnalysisRunStatus.Failed);
        persistedRun.RegisteredSolutionId.Should().Be(fixture.InvalidSolution.Id);
    }

    [Fact]
    public async Task StartRunAsync_ShouldThrowClearlyForUnknownRegisteredSolution()
    {
        await using var fixture = await CoverageAnalysisRunServiceFixture.CreateAsync();
        var unknownSolutionId = Guid.NewGuid();

        var action = async () => await fixture.Service.StartRunAsync(unknownSolutionId);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Registered solution '{unknownSolutionId}' was not found.*");
        fixture.Analyzer.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task StartRunAsync_ShouldCreateFailedRunWhenAnalyzerThrows()
    {
        await using var fixture = await CoverageAnalysisRunServiceFixture.CreateAsync();
        fixture.Analyzer.ExceptionToThrow = new InvalidOperationException("dotnet test failed with exit code 1.");

        var run = await fixture.Service.StartRunAsync(fixture.ValidSolution.Id);

        run.Status.Should().Be(CoverageAnalysisRunStatus.Failed);
        run.ErrorMessage.Should().Be("dotnet test failed with exit code 1.");
        run.ProjectCount.Should().Be(0);
        run.ClassCount.Should().Be(0);
        run.ProjectSummaries.Should().BeEmpty();
        run.ClassCoverages.Should().BeEmpty();
        fixture.Analyzer.Calls.Should().ContainSingle();

        var persistedRun = await fixture.DbContext.CoverageAnalysisRuns.SingleAsync(existing => existing.Id == run.Id);
        persistedRun.Status.Should().Be(CoverageAnalysisRunStatus.Failed);
        persistedRun.ErrorMessage.Should().Be("dotnet test failed with exit code 1.");
    }

    [Fact]
    public async Task ServiceProvider_ShouldResolveManualCoverageRunServicesWithScopedDependencies()
    {
        var databaseName = $"codepass-coverage-analysis-run-di-{Guid.NewGuid():N}";
        var fakeAnalyzer = new FakeCoverageAnalyzer { ResultToReturn = new CoverageAnalysisResultModel([], []) };
        var now = DateTimeOffset.UtcNow;
        var solution = CoverageAnalysisRunServiceFixture.CreateSolution(
            "DI Solution",
            "/tmp/coverage-di-solution.sln",
            RegisteredSolutionStatus.Valid,
            now);

        var services = new ServiceCollection();
        services.AddDbContext<CodePassDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<ISolutionPathValidator, SolutionPathValidator>();
        services.AddScoped<IRegisteredSolutionService, RegisteredSolutionService>();
        services.AddScoped<ICoverageAnalysisResultService, CoverageAnalysisResultService>();
        services.AddScoped<ICoverageAnalyzer>(_ => fakeAnalyzer);
        services.AddScoped<ICoverageAnalysisRunService, CoverageAnalysisRunService>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using (var seedScope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<CodePassDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.RegisteredSolutions.Add(solution);
            await dbContext.SaveChangesAsync();
        }

        await using var runScope = serviceProvider.CreateAsyncScope();
        var runService = runScope.ServiceProvider.GetRequiredService<ICoverageAnalysisRunService>();
        var resultService = runScope.ServiceProvider.GetRequiredService<ICoverageAnalysisResultService>();
        var analyzer = runScope.ServiceProvider.GetRequiredService<ICoverageAnalyzer>();

        resultService.Should().NotBeNull();
        analyzer.Should().BeSameAs(fakeAnalyzer);
        var run = await runService.StartRunAsync(solution.Id);

        run.Status.Should().Be(CoverageAnalysisRunStatus.Succeeded);
        fakeAnalyzer.Calls.Should().ContainSingle(call => call.SolutionPath == solution.SolutionPath);
    }

    private static CoverageAnalysisResultModel CreateCoverageResult() => new(
        [new CoverageProjectSummaryModel("Alpha.Project", 15, 20, 75, 5, 6, 83.33)],
        [
            new CoverageClassCoverageModel("Alpha.Project", "Alpha.Project.SecondClass", "src/SecondClass.cs", 5, 10, 50, 1, 2, 50),
            new CoverageClassCoverageModel("Alpha.Project", "Alpha.Project.FirstClass", "src/FirstClass.cs", 10, 10, 100, 4, 4, 100)
        ]);
}

internal sealed class CoverageAnalysisRunServiceFixture : IAsyncDisposable
{
    private CoverageAnalysisRunServiceFixture(
        CodePassDbContext dbContext,
        ICoverageAnalysisRunService service,
        FakeCoverageAnalyzer analyzer,
        RegisteredSolution validSolution,
        RegisteredSolution invalidSolution)
    {
        DbContext = dbContext;
        Service = service;
        Analyzer = analyzer;
        ValidSolution = validSolution;
        InvalidSolution = invalidSolution;
    }

    public CodePassDbContext DbContext { get; }

    public ICoverageAnalysisRunService Service { get; }

    public FakeCoverageAnalyzer Analyzer { get; }

    public RegisteredSolution ValidSolution { get; }

    public RegisteredSolution InvalidSolution { get; }

    public static async Task<CoverageAnalysisRunServiceFixture> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseInMemoryDatabase($"codepass-coverage-analysis-run-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var validSolution = CreateSolution("Coverage Solution", "/tmp/coverage-solution.sln", RegisteredSolutionStatus.Valid, now);
        var invalidSolution = CreateSolution("Missing Coverage Solution", "/tmp/missing-coverage.sln", RegisteredSolutionStatus.FileNotFound, now);

        dbContext.RegisteredSolutions.AddRange(validSolution, invalidSolution);
        await dbContext.SaveChangesAsync();

        var analyzer = new FakeCoverageAnalyzer();
        var service = new CoverageAnalysisRunService(
            new RegisteredSolutionService(dbContext, new SolutionPathValidator()),
            analyzer,
            new CoverageAnalysisResultService(dbContext));

        return new CoverageAnalysisRunServiceFixture(
            dbContext,
            service,
            analyzer,
            validSolution,
            invalidSolution);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }

    public static RegisteredSolution CreateSolution(
        string displayName,
        string solutionPath,
        RegisteredSolutionStatus status,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = displayName,
        SolutionPath = solutionPath,
        Status = status,
        StatusMessage = status == RegisteredSolutionStatus.Valid ? null : "Solution file was not found.",
        LastValidatedAtUtc = now,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };
}

internal sealed class FakeCoverageAnalyzer : ICoverageAnalyzer
{
    public List<CoverageAnalyzeCall> Calls { get; } = [];

    public CoverageAnalysisResultModel ResultToReturn { get; set; } = new([], []);

    public Exception? ExceptionToThrow { get; set; }

    public Task<CoverageAnalysisResultModel> AnalyzeAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        Calls.Add(new CoverageAnalyzeCall(solutionPath));

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(ResultToReturn);
    }
}

internal sealed record CoverageAnalyzeCall(string SolutionPath);
