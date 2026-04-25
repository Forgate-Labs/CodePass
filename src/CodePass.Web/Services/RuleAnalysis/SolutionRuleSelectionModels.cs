using CodePass.Web.Data.Entities;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed record SolutionRuleSelectionDto(
    Guid RuleId,
    string RuleCode,
    string Title,
    RuleSeverity Severity,
    string RuleKind,
    bool IsGloballyEnabled,
    bool IsEnabledForSolution,
    DateTimeOffset? UpdatedAtUtc);

public sealed record SetSolutionRuleSelectionRequest(
    Guid RegisteredSolutionId,
    Guid AuthoredRuleDefinitionId,
    bool IsEnabled);
