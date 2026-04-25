using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed class RuleAnalysisResultService(CodePassDbContext dbContext) : IRuleAnalysisResultService
{
    public async Task<RuleAnalysisRunDto> CreateRunningRunAsync(
        Guid registeredSolutionId,
        int ruleCount,
        CancellationToken cancellationToken = default)
    {
        if (ruleCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ruleCount), ruleCount, "Rule count cannot be negative.");
        }

        await EnsureRegisteredSolutionExistsAsync(registeredSolutionId, cancellationToken);

        var run = new RuleAnalysisRun
        {
            Id = Guid.NewGuid(),
            RegisteredSolutionId = registeredSolutionId,
            Status = RuleAnalysisRunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
            RuleCount = ruleCount,
            TotalViolations = 0
        };

        dbContext.RuleAnalysisRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapRun(run);
    }

    public async Task<RuleAnalysisRunDto> MarkSucceededAsync(
        Guid runId,
        IReadOnlyList<RuleAnalysisFinding> findings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(findings);

        var run = await GetTrackedRunAsync(runId, cancellationToken);
        await RemoveExistingViolationsAsync(runId, cancellationToken);

        var ruleIds = findings.Select(finding => finding.RuleId).Distinct().ToArray();
        var existingRuleIds = ruleIds.Length == 0
            ? new HashSet<Guid>()
            : await dbContext.AuthoredRuleDefinitions
                .AsNoTracking()
                .Where(rule => ruleIds.Contains(rule.Id))
                .Select(rule => rule.Id)
                .ToHashSetAsync(cancellationToken);

        foreach (var finding in findings)
        {
            dbContext.RuleAnalysisViolations.Add(new RuleAnalysisViolation
            {
                Id = Guid.NewGuid(),
                RuleAnalysisRunId = run.Id,
                AuthoredRuleDefinitionId = existingRuleIds.Contains(finding.RuleId) ? finding.RuleId : null,
                RuleCode = finding.RuleCode,
                RuleTitle = finding.RuleTitle,
                RuleKind = finding.RuleKind,
                RuleSeverity = finding.Severity,
                Message = finding.Message,
                FilePath = finding.RelativeFilePath,
                StartLine = finding.StartLine,
                StartColumn = finding.StartColumn,
                EndLine = finding.EndLine,
                EndColumn = finding.EndColumn
            });
        }

        run.Status = RuleAnalysisRunStatus.Succeeded;
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.TotalViolations = findings.Count;
        run.ErrorMessage = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await GetRunAsync(runId, cancellationToken);
        return dto ?? throw new InvalidOperationException($"Rule-analysis run '{runId}' was not found after completion.");
    }

    public async Task<RuleAnalysisRunDto> MarkFailedAsync(
        Guid runId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        var run = await GetTrackedRunAsync(runId, cancellationToken);
        await RemoveExistingViolationsAsync(runId, cancellationToken);

        run.Status = RuleAnalysisRunStatus.Failed;
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        run.TotalViolations = 0;
        run.ErrorMessage = errorMessage;

        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await GetRunAsync(runId, cancellationToken);
        return dto ?? throw new InvalidOperationException($"Rule-analysis run '{runId}' was not found after failure.");
    }

    public async Task<RuleAnalysisRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await dbContext.RuleAnalysisRuns
            .AsNoTracking()
            .Include(existing => existing.Violations)
            .SingleOrDefaultAsync(existing => existing.Id == runId, cancellationToken);

        return run is null ? null : MapRun(run);
    }

    public async Task<RuleAnalysisRunDto?> GetLatestRunForSolutionAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default)
    {
        var run = await dbContext.RuleAnalysisRuns
            .AsNoTracking()
            .Include(existing => existing.Violations)
            .Where(existing => existing.RegisteredSolutionId == registeredSolutionId)
            .OrderByDescending(existing => existing.StartedAtUtc)
            .ThenByDescending(existing => existing.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return run is null ? null : MapRun(run);
    }

    private async Task<RuleAnalysisRun> GetTrackedRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await dbContext.RuleAnalysisRuns.SingleOrDefaultAsync(existing => existing.Id == runId, cancellationToken);

        return run ?? throw new InvalidOperationException($"Rule-analysis run '{runId}' was not found.");
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

    private async Task RemoveExistingViolationsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var existingViolations = await dbContext.RuleAnalysisViolations
            .Where(violation => violation.RuleAnalysisRunId == runId)
            .ToListAsync(cancellationToken);

        if (existingViolations.Count > 0)
        {
            dbContext.RuleAnalysisViolations.RemoveRange(existingViolations);
        }
    }

    private static RuleAnalysisRunDto MapRun(RuleAnalysisRun run)
    {
        var groups = run.Violations
            .GroupBy(violation => new
            {
                violation.RuleCode,
                violation.RuleTitle,
                violation.RuleKind,
                violation.RuleSeverity
            })
            .OrderByDescending(group => group.Key.RuleSeverity)
            .ThenBy(group => group.Key.RuleCode, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var violations = group
                    .OrderBy(violation => violation.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(violation => violation.StartLine)
                    .ThenBy(violation => violation.StartColumn)
                    .Select(violation => new RuleAnalysisViolationDto(
                        violation.Id,
                        violation.AuthoredRuleDefinitionId,
                        violation.RuleCode,
                        violation.RuleTitle,
                        violation.RuleKind,
                        violation.RuleSeverity,
                        violation.Message,
                        violation.FilePath,
                        violation.StartLine,
                        violation.StartColumn,
                        violation.EndLine,
                        violation.EndColumn))
                    .ToList();

                return new RuleAnalysisRuleGroupDto(
                    group.Key.RuleCode,
                    group.Key.RuleTitle,
                    group.Key.RuleKind,
                    group.Key.RuleSeverity,
                    violations.Count,
                    violations);
            })
            .ToList();

        return new RuleAnalysisRunDto(
            run.Id,
            run.RegisteredSolutionId,
            run.Status,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.RuleCount,
            run.TotalViolations,
            run.ErrorMessage,
            groups);
    }
}
