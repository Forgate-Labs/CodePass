using CodePass.Web.Data.Entities;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed record RuleAnalysisFinding(
    Guid RuleId,
    string RuleCode,
    string RuleTitle,
    string RuleKind,
    RuleSeverity Severity,
    string Message,
    string RelativeFilePath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);
