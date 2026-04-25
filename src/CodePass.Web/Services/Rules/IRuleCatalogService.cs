namespace CodePass.Web.Services.Rules;

public interface IRuleCatalogService
{
    Task<IReadOnlyList<RuleKindCatalogEntry>> GetRuleKindsAsync(CancellationToken cancellationToken = default);
    Task<RuleKindCatalogEntry?> GetRuleKindAsync(string kind, CancellationToken cancellationToken = default);
}
