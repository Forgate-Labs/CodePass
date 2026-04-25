using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Rules;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed class SolutionRuleSelectionService(CodePassDbContext dbContext) : ISolutionRuleSelectionService
{
    public async Task<IReadOnlyList<SolutionRuleSelectionDto>> GetSelectionsAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        await EnsureRegisteredSolutionExistsAsync(registeredSolutionId, cancellationToken);

        var rules = await dbContext.AuthoredRuleDefinitions
            .AsNoTracking()
            .OrderBy(rule => rule.Title)
            .ThenBy(rule => rule.Code)
            .ToListAsync(cancellationToken);

        var assignments = await dbContext.SolutionRuleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.RegisteredSolutionId == registeredSolutionId)
            .ToDictionaryAsync(assignment => assignment.AuthoredRuleDefinitionId, cancellationToken);

        return rules
            .Select(rule =>
            {
                assignments.TryGetValue(rule.Id, out var assignment);

                return new SolutionRuleSelectionDto(
                    RuleId: rule.Id,
                    RuleCode: rule.Code,
                    Title: rule.Title,
                    Severity: rule.Severity,
                    RuleKind: rule.RuleKind,
                    IsGloballyEnabled: rule.IsEnabled,
                    IsEnabledForSolution: assignment?.IsEnabled ?? false,
                    UpdatedAtUtc: assignment?.UpdatedAtUtc);
            })
            .ToList();
    }

    public async Task SetRuleEnabledAsync(SetSolutionRuleSelectionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureRegisteredSolutionExistsAsync(request.RegisteredSolutionId, cancellationToken);
        await EnsureAuthoredRuleDefinitionExistsAsync(request.AuthoredRuleDefinitionId, cancellationToken);

        var assignment = await dbContext.SolutionRuleAssignments
            .SingleOrDefaultAsync(
                existing => existing.RegisteredSolutionId == request.RegisteredSolutionId
                    && existing.AuthoredRuleDefinitionId == request.AuthoredRuleDefinitionId,
                cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (assignment is null)
        {
            dbContext.SolutionRuleAssignments.Add(new SolutionRuleAssignment
            {
                Id = Guid.NewGuid(),
                RegisteredSolutionId = request.RegisteredSolutionId,
                AuthoredRuleDefinitionId = request.AuthoredRuleDefinitionId,
                IsEnabled = request.IsEnabled,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        else
        {
            assignment.IsEnabled = request.IsEnabled;
            assignment.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetEnabledRuleDefinitionsForSolutionAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        await EnsureRegisteredSolutionExistsAsync(registeredSolutionId, cancellationToken);

        return await dbContext.SolutionRuleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.RegisteredSolutionId == registeredSolutionId && assignment.IsEnabled)
            .Join(
                dbContext.AuthoredRuleDefinitions.AsNoTracking().Where(rule => rule.IsEnabled),
                assignment => assignment.AuthoredRuleDefinitionId,
                rule => rule.Id,
                (assignment, rule) => new AuthoredRuleDefinitionDto(
                    rule.Id,
                    rule.Code,
                    rule.Title,
                    rule.Description,
                    rule.RuleKind,
                    rule.SchemaVersion,
                    rule.Severity,
                    rule.ScopeJson,
                    rule.ParametersJson,
                    rule.RawDefinitionJson,
                    rule.IsEnabled,
                    rule.CreatedAtUtc,
                    rule.UpdatedAtUtc))
            .OrderBy(rule => rule.Title)
            .ThenBy(rule => rule.Code)
            .ToListAsync(cancellationToken);
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

    private async Task EnsureAuthoredRuleDefinitionExistsAsync(Guid authoredRuleDefinitionId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.AuthoredRuleDefinitions
            .AsNoTracking()
            .AnyAsync(rule => rule.Id == authoredRuleDefinitionId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException($"Authored rule definition '{authoredRuleDefinitionId}' was not found.");
        }
    }
}
