namespace CodePass.Web.Services.RuleAnalysis;

public interface IRuleAnalysisRunService
{
    Task<RuleAnalysisRunDto> StartRunAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default);
}
