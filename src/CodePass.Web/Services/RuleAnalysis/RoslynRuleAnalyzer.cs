using CodePass.Web.Services.Rules;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed class RoslynRuleAnalyzer : IRuleAnalyzer
{
    public Task<IReadOnlyList<RuleAnalysisFinding>> AnalyzeAsync(
        string solutionPath,
        IReadOnlyList<AuthoredRuleDefinitionDto> rules,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<RuleAnalysisFinding>>(Array.Empty<RuleAnalysisFinding>());
    }
}
