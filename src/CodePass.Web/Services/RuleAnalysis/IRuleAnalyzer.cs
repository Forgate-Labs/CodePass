using CodePass.Web.Services.Rules;

namespace CodePass.Web.Services.RuleAnalysis;

public interface IRuleAnalyzer
{
    Task<IReadOnlyList<RuleAnalysisFinding>> AnalyzeAsync(
        string solutionPath,
        IReadOnlyList<AuthoredRuleDefinitionDto> rules,
        CancellationToken cancellationToken = default,
        IProgress<RuleAnalysisProgressDto>? progress = null);
}
