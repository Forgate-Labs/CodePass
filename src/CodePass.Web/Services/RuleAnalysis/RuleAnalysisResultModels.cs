using CodePass.Web.Data.Entities;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed record RuleAnalysisRunDto(
    Guid Id,
    Guid RegisteredSolutionId,
    RuleAnalysisRunStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int RuleCount,
    int TotalViolations,
    string? ErrorMessage,
    IReadOnlyList<RuleAnalysisRuleGroupDto> RuleGroups);

public sealed record RuleAnalysisRuleGroupDto(
    string RuleCode,
    string RuleTitle,
    string RuleKind,
    RuleSeverity Severity,
    int ViolationCount,
    IReadOnlyList<RuleAnalysisViolationDto> Violations);

public sealed record RuleAnalysisViolationDto(
    Guid Id,
    Guid? AuthoredRuleDefinitionId,
    string RuleCode,
    string RuleTitle,
    string RuleKind,
    RuleSeverity Severity,
    string Message,
    string FilePath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);
