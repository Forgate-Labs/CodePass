namespace CodePass.Web.Data.Entities;

public sealed class SolutionRuleAssignment
{
    public Guid Id { get; set; }

    public Guid RegisteredSolutionId { get; set; }

    public Guid AuthoredRuleDefinitionId { get; set; }

    public bool IsEnabled { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
