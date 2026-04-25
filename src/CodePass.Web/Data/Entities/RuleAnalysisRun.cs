namespace CodePass.Web.Data.Entities;

public sealed class RuleAnalysisRun
{
    public Guid Id { get; set; }

    public Guid RegisteredSolutionId { get; set; }

    public RuleAnalysisRunStatus Status { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public int RuleCount { get; set; }

    public int TotalViolations { get; set; }

    public string? ErrorMessage { get; set; }

    public ICollection<RuleAnalysisViolation> Violations { get; } = new List<RuleAnalysisViolation>();
}
