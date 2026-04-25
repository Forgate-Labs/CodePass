using System.ComponentModel.DataAnnotations;

namespace CodePass.Web.Data.Entities;

public sealed class AuthoredRuleDefinition
{
    public Guid Id { get; set; }

    [MaxLength(64)]
    public required string Code { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public required string RuleKind { get; set; }

    [MaxLength(32)]
    public required string SchemaVersion { get; set; }

    public RuleSeverity Severity { get; set; }

    public required string ScopeJson { get; set; }

    public required string ParametersJson { get; set; }

    public required string RawDefinitionJson { get; set; }

    public bool IsEnabled { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
