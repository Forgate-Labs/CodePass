using CodePass.Web.Services.Rules;

namespace CodePass.Web.Services.RuleAnalysis;

public interface ISolutionRuleSelectionService
{
    Task<IReadOnlyList<SolutionRuleSelectionDto>> GetSelectionsAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default);

    Task SetRuleEnabledAsync(SetSolutionRuleSelectionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetEnabledRuleDefinitionsForSolutionAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default);
}
