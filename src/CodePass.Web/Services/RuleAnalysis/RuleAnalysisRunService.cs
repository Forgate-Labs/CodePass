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
    public async Task<RuleAnalysisRunDto> StartRunAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default,
        IProgress<RuleAnalysisProgressDto>? progress = null)
    {
        Report(progress, RuleAnalysisProgressStage.Preparing, "Preparing rule analysis...", percentComplete: 5);

        var registeredSolution = await dbContext.RegisteredSolutions
            .AsNoTracking()
            .SingleOrDefaultAsync(solution => solution.Id == registeredSolutionId, cancellationToken);

        if (registeredSolution is null)
        {
            Report(progress, RuleAnalysisProgressStage.Failed, "Registered solution was not found.", percentComplete: 100);
            throw new InvalidOperationException($"Registered solution '{registeredSolutionId}' was not found.");
        }

        if (registeredSolution.Status != RegisteredSolutionStatus.Valid)
        {
            Report(progress, RuleAnalysisProgressStage.CreatingRun, "Recording failed analysis run for an invalid solution...", percentComplete: 20);
            var failedRun = await resultService.CreateRunningRunAsync(registeredSolution.Id, ruleCount: 0, cancellationToken);
            var result = await resultService.MarkFailedAsync(
                failedRun.Id,
                $"Only valid registered solutions can be analyzed. Current status: {registeredSolution.Status}.",
                cancellationToken);
            Report(progress, RuleAnalysisProgressStage.Failed, "Rule analysis cannot run for the selected solution.", percentComplete: 100, detail: result.ErrorMessage);
            return result;
        }

        Report(progress, RuleAnalysisProgressStage.LoadingRules, "Loading enabled authored rules for this solution...", percentComplete: 10);
        var enabledRules = await ruleSelectionService.GetEnabledRuleDefinitionsForSolutionAsync(
            registeredSolution.Id,
            cancellationToken);

        Report(progress, RuleAnalysisProgressStage.CreatingRun, $"Creating rule-analysis run for {enabledRules.Count} enabled rule(s)...", percentComplete: 15);
        var runningRun = await resultService.CreateRunningRunAsync(
            registeredSolution.Id,
            enabledRules.Count,
            cancellationToken);

        if (enabledRules.Count == 0)
        {
            Report(progress, RuleAnalysisProgressStage.SavingResults, "No enabled rules found; saving a zero-violation run...", percentComplete: 95);
            var result = await resultService.MarkSucceededAsync(runningRun.Id, [], cancellationToken);
            Report(progress, RuleAnalysisProgressStage.Completed, "Rule analysis completed with no enabled rules.", percentComplete: 100);
            return result;
        }

        try
        {
            Report(progress, RuleAnalysisProgressStage.LoadingSolution, "Loading solution with Roslyn/MSBuild...", percentComplete: 20, detail: registeredSolution.SolutionPath);
            var findings = await ruleAnalyzer.AnalyzeAsync(
                registeredSolution.SolutionPath,
                enabledRules,
                cancellationToken,
                progress);

            Report(progress, RuleAnalysisProgressStage.SavingResults, $"Saving {findings.Count} rule-analysis finding(s)...", percentComplete: 95);
            var result = await resultService.MarkSucceededAsync(runningRun.Id, findings, cancellationToken);
            Report(progress, RuleAnalysisProgressStage.Completed, "Rule analysis completed.", percentComplete: 100, detail: $"{result.TotalViolations} violation(s) found.");
            return result;
        }
        catch (Exception exception)
        {
            var errorMessage = string.IsNullOrWhiteSpace(exception.Message)
                ? "Rule analysis failed."
                : exception.Message;

            Report(progress, RuleAnalysisProgressStage.Failed, "Rule analysis failed.", percentComplete: 100, detail: errorMessage);
            return await resultService.MarkFailedAsync(runningRun.Id, errorMessage, CancellationToken.None);
        }
    }

    private static void Report(
        IProgress<RuleAnalysisProgressDto>? progress,
        RuleAnalysisProgressStage stage,
        string message,
        int? percentComplete = null,
        int? current = null,
        int? total = null,
        string? detail = null)
        => progress?.Report(new RuleAnalysisProgressDto(
            stage,
            message,
            percentComplete,
            current,
            total,
            detail));
}
