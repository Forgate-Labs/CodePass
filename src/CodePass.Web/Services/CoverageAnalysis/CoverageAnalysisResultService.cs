using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Services.CoverageAnalysis;

public sealed class CoverageAnalysisResultService(CodePassDbContext dbContext) : ICoverageAnalysisResultService
{
    public async Task<CoverageAnalysisRunDto> CreateRunningRunAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRegisteredSolutionExistsAsync(registeredSolutionId, cancellationToken);

        var run = new CoverageAnalysisRun
        {
            Id = Guid.NewGuid(),
            RegisteredSolutionId = registeredSolutionId,
            Status = CoverageAnalysisRunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
            ProjectCount = 0,
            ClassCount = 0,
            CoveredLineCount = 0,
            TotalLineCount = 0,
            LineCoveragePercent = 0,
            CoveredBranchCount = 0,
            TotalBranchCount = 0,
            BranchCoveragePercent = 0
        };

        dbContext.CoverageAnalysisRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapRun(run);
    }

    public async Task<CoverageAnalysisRunDto> MarkSucceededAsync(
        Guid runId,
        CoverageAnalysisResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var run = await GetTrackedRunAsync(runId, cancellationToken);
        await RemoveExistingChildRowsAsync(runId, cancellationToken);

        foreach (var project in result.Projects)
        {
            dbContext.CoverageProjectSummaries.Add(new CodePass.Web.Data.Entities.CoverageProjectSummary
            {
                Id = Guid.NewGuid(),
                CoverageAnalysisRunId = run.Id,
                ProjectName = project.ProjectName,
                CoveredLineCount = project.CoveredLines,
                TotalLineCount = project.TotalLines,
                LineCoveragePercent = project.LineCoveragePercent,
                CoveredBranchCount = project.CoveredBranches,
                TotalBranchCount = project.TotalBranches,
                BranchCoveragePercent = project.BranchCoveragePercent
            });
        }

        foreach (var classCoverage in result.Classes)
        {
            dbContext.CoverageClassCoverages.Add(new CodePass.Web.Data.Entities.CoverageClassCoverage
            {
                Id = Guid.NewGuid(),
                CoverageAnalysisRunId = run.Id,
                ProjectName = classCoverage.ProjectName,
                ClassName = classCoverage.ClassName,
                FilePath = classCoverage.FilePath,
                CoveredLineCount = classCoverage.CoveredLines,
                TotalLineCount = classCoverage.TotalLines,
                LineCoveragePercent = classCoverage.LineCoveragePercent,
                CoveredBranchCount = classCoverage.CoveredBranches,
                TotalBranchCount = classCoverage.TotalBranches,
                BranchCoveragePercent = classCoverage.BranchCoveragePercent
            });
        }

        run.Status = CoverageAnalysisRunStatus.Succeeded;
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.ProjectCount = result.Projects.Count;
        run.ClassCount = result.Classes.Count;
        run.CoveredLineCount = result.Projects.Sum(project => project.CoveredLines);
        run.TotalLineCount = result.Projects.Sum(project => project.TotalLines);
        run.LineCoveragePercent = CalculateCoveragePercent(run.CoveredLineCount, run.TotalLineCount);
        run.CoveredBranchCount = result.Projects.Sum(project => project.CoveredBranches);
        run.TotalBranchCount = result.Projects.Sum(project => project.TotalBranches);
        run.BranchCoveragePercent = CalculateCoveragePercent(run.CoveredBranchCount, run.TotalBranchCount);
        run.ErrorMessage = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await GetRunAsync(runId, cancellationToken);
        return dto ?? throw new InvalidOperationException($"Coverage-analysis run '{runId}' was not found after completion.");
    }

    public async Task<CoverageAnalysisRunDto> MarkFailedAsync(
        Guid runId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        var run = await GetTrackedRunAsync(runId, cancellationToken);
        await RemoveExistingChildRowsAsync(runId, cancellationToken);

        run.Status = CoverageAnalysisRunStatus.Failed;
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.ProjectCount = 0;
        run.ClassCount = 0;
        run.CoveredLineCount = 0;
        run.TotalLineCount = 0;
        run.LineCoveragePercent = 0;
        run.CoveredBranchCount = 0;
        run.TotalBranchCount = 0;
        run.BranchCoveragePercent = 0;
        run.ErrorMessage = errorMessage;

        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await GetRunAsync(runId, cancellationToken);
        return dto ?? throw new InvalidOperationException($"Coverage-analysis run '{runId}' was not found after failure.");
    }

    public async Task<CoverageAnalysisRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await dbContext.CoverageAnalysisRuns
            .AsNoTracking()
            .Include(existing => existing.ProjectSummaries)
            .Include(existing => existing.ClassCoverages)
            .SingleOrDefaultAsync(existing => existing.Id == runId, cancellationToken);

        return run is null ? null : MapRun(run);
    }

    public async Task<CoverageAnalysisRunDto?> GetLatestRunForSolutionAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default)
    {
        var runs = await dbContext.CoverageAnalysisRuns
            .AsNoTracking()
            .Include(existing => existing.ProjectSummaries)
            .Include(existing => existing.ClassCoverages)
            .Where(existing => existing.RegisteredSolutionId == registeredSolutionId)
            .ToListAsync(cancellationToken);

        var latestRun = runs
            .OrderByDescending(existing => existing.StartedAtUtc)
            .ThenByDescending(existing => existing.Id)
            .FirstOrDefault();

        return latestRun is null ? null : MapRun(latestRun);
    }

    private async Task<CoverageAnalysisRun> GetTrackedRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await dbContext.CoverageAnalysisRuns.SingleOrDefaultAsync(existing => existing.Id == runId, cancellationToken);

        return run ?? throw new InvalidOperationException($"Coverage-analysis run '{runId}' was not found.");
    }

    private async Task EnsureRegisteredSolutionExistsAsync(Guid registeredSolutionId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.RegisteredSolutions
            .AsNoTracking()
            .AnyAsync(solution => solution.Id == registeredSolutionId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException($"Registered solution '{registeredSolutionId}' was not found.");
        }
    }

    private async Task RemoveExistingChildRowsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var existingProjectSummaries = await dbContext.CoverageProjectSummaries
            .Where(summary => summary.CoverageAnalysisRunId == runId)
            .ToListAsync(cancellationToken);

        if (existingProjectSummaries.Count > 0)
        {
            dbContext.CoverageProjectSummaries.RemoveRange(existingProjectSummaries);
        }

        var existingClassCoverages = await dbContext.CoverageClassCoverages
            .Where(classCoverage => classCoverage.CoverageAnalysisRunId == runId)
            .ToListAsync(cancellationToken);

        if (existingClassCoverages.Count > 0)
        {
            dbContext.CoverageClassCoverages.RemoveRange(existingClassCoverages);
        }
    }

    private static CoverageAnalysisRunDto MapRun(CoverageAnalysisRun run)
    {
        var projectSummaries = run.ProjectSummaries
            .OrderBy(summary => summary.ProjectName, StringComparer.Ordinal)
            .Select(summary => new CoverageProjectSummaryDto(
                summary.Id,
                summary.ProjectName,
                summary.CoveredLineCount,
                summary.TotalLineCount,
                summary.LineCoveragePercent,
                summary.CoveredBranchCount,
                summary.TotalBranchCount,
                summary.BranchCoveragePercent))
            .ToList();

        var classCoverages = run.ClassCoverages
            .OrderBy(classCoverage => classCoverage.ProjectName, StringComparer.Ordinal)
            .ThenBy(classCoverage => classCoverage.ClassName, StringComparer.Ordinal)
            .ThenBy(classCoverage => classCoverage.FilePath, StringComparer.Ordinal)
            .Select(classCoverage => new CoverageClassCoverageDto(
                classCoverage.Id,
                classCoverage.ProjectName,
                classCoverage.ClassName,
                classCoverage.FilePath,
                classCoverage.CoveredLineCount,
                classCoverage.TotalLineCount,
                classCoverage.LineCoveragePercent,
                classCoverage.CoveredBranchCount,
                classCoverage.TotalBranchCount,
                classCoverage.BranchCoveragePercent))
            .ToList();

        return new CoverageAnalysisRunDto(
            run.Id,
            run.RegisteredSolutionId,
            run.Status,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.ProjectCount,
            run.ClassCount,
            run.CoveredLineCount,
            run.TotalLineCount,
            run.LineCoveragePercent,
            run.CoveredBranchCount,
            run.TotalBranchCount,
            run.BranchCoveragePercent,
            run.ErrorMessage,
            projectSummaries,
            classCoverages);
    }

    private static double CalculateCoveragePercent(int coveredCount, int totalCount)
    {
        return totalCount == 0 ? 0 : Math.Round(coveredCount * 100d / totalCount, 2, MidpointRounding.AwayFromZero);
    }
}
