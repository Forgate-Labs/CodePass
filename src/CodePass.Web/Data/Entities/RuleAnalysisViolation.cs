namespace CodePass.Web.Data.Entities;

public sealed class RuleAnalysisViolation
{
    public Guid Id { get; set; }

    public Guid RuleAnalysisRunId { get; set; }

    public Guid? AuthoredRuleDefinitionId { get; set; }

    public required string RuleCode { get; set; }

    public required string RuleTitle { get; set; }

    public required string RuleKind { get; set; }

    public RuleSeverity RuleSeverity { get; set; }

    public required string Message { get; set; }

    public required string FilePath { get; set; }

    public int StartLine { get; set; }

    public int StartColumn { get; set; }

    public int EndLine { get; set; }

    public int EndColumn { get; set; }

    public RuleAnalysisRun? RuleAnalysisRun { get; set; }
}
