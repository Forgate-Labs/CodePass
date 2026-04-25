using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed class RuleAnalysisRunService(
    CodePassDbContext dbContext,
    ISolutionRuleSelectionService ruleSelectionService,
    IRuleAnalyzer ruleAnalyzer,
    IRuleAnalysisResultService resultService) : IRuleAnalysisRunService
{
    public async Task<RuleAnalysisRunDto> StartRunAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        var registeredSolution = await dbContext.RegisteredSolutions
            .AsNoTracking()
            .SingleOrDefaultAsync(solution => solution.Id == registeredSolutionId, cancellationToken);

        if (registeredSolution is null)
        {
            throw new InvalidOperationException($"Registered solution '{registeredSolutionId}' was not found.");
        }

        if (registeredSolution.Status != RegisteredSolutionStatus.Valid)
        {
            var failedRun = await resultService.CreateRunningRunAsync(registeredSolution.Id, ruleCount: 0, cancellationToken);
            return await resultService.MarkFailedAsync(
                failedRun.Id,
                $"Only valid registered solutions can be analyzed. Current status: {registeredSolution.Status}.",
                cancellationToken);
        }

        var enabledRules = await ruleSelectionService.GetEnabledRuleDefinitionsForSolutionAsync(
            registeredSolution.Id,
            cancellationToken);
        var runningRun = await resultService.CreateRunningRunAsync(
            registeredSolution.Id,
            enabledRules.Count,
            cancellationToken);

        if (enabledRules.Count == 0)
        {
            return await resultService.MarkSucceededAsync(runningRun.Id, [], cancellationToken);
        }

        try
        {
            var findings = await ruleAnalyzer.AnalyzeAsync(
                registeredSolution.SolutionPath,
                enabledRules,
                cancellationToken);

            return await resultService.MarkSucceededAsync(runningRun.Id, findings, cancellationToken);
        }
        catch (Exception exception)
        {
            var errorMessage = string.IsNullOrWhiteSpace(exception.Message)
                ? "Rule analysis failed."
                : exception.Message;

            return await resultService.MarkFailedAsync(runningRun.Id, errorMessage, CancellationToken.None);
        }
    }
}
