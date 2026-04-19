using System.ComponentModel.DataAnnotations;

namespace CodePass.Web.Data.Entities;

public sealed class RegisteredSolution
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public required string DisplayName { get; set; }

    [MaxLength(2048)]
    public required string SolutionPath { get; set; }

    public RegisteredSolutionStatus Status { get; set; }

    [MaxLength(500)]
    public string? StatusMessage { get; set; }

    public DateTimeOffset? LastValidatedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
