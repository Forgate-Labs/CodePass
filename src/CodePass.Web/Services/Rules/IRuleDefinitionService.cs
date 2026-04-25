namespace CodePass.Web.Services.Rules;

public interface IRuleDefinitionService
{
    Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AuthoredRuleDefinitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AuthoredRuleDefinitionDto> CreateAsync(SaveAuthoredRuleDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<AuthoredRuleDefinitionDto> UpdateAsync(Guid id, SaveAuthoredRuleDefinitionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default);
}
