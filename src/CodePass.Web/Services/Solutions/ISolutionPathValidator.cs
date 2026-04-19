using CodePass.Web.Data.Entities;

namespace CodePass.Web.Services.Solutions;

public interface ISolutionPathValidator
{
    Task<SolutionPathValidationResult> ValidateAsync(string? solutionPath, CancellationToken cancellationToken = default);
}

public sealed record SolutionPathValidationResult(
    RegisteredSolutionStatus Status,
    string? CanonicalPath,
    string? Message)
{
    public bool IsValid => Status == RegisteredSolutionStatus.Valid;
}
