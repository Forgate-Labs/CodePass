using CodePass.Web.Data.Entities;

namespace CodePass.Web.Services.Solutions;

public sealed class SolutionPathValidator : ISolutionPathValidator
{
    public Task<SolutionPathValidationResult> ValidateAsync(string? solutionPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return Task.FromResult(new SolutionPathValidationResult(
                RegisteredSolutionStatus.Invalid,
                null,
                "A solution path is required."));
        }

        string canonicalPath;

        try
        {
            canonicalPath = Path.GetFullPath(solutionPath.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Task.FromResult(new SolutionPathValidationResult(
                RegisteredSolutionStatus.Invalid,
                null,
                "The provided path is invalid."));
        }

        if (!string.Equals(Path.GetExtension(canonicalPath), ".sln", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new SolutionPathValidationResult(
                RegisteredSolutionStatus.Invalid,
                canonicalPath,
                "Only direct .sln file paths are supported."));
        }

        if (!File.Exists(canonicalPath))
        {
            return Task.FromResult(new SolutionPathValidationResult(
                RegisteredSolutionStatus.FileNotFound,
                canonicalPath,
                "The solution file was not found."));
        }

        try
        {
            using var stream = new FileStream(canonicalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Task.FromResult(new SolutionPathValidationResult(
                RegisteredSolutionStatus.Valid,
                canonicalPath,
                "The solution file is accessible."));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new SolutionPathValidationResult(
                RegisteredSolutionStatus.PathInaccessible,
                canonicalPath,
                "The solution file is not accessible to CodePass."));
        }
        catch (IOException)
        {
            return Task.FromResult(new SolutionPathValidationResult(
                RegisteredSolutionStatus.PathInaccessible,
                canonicalPath,
                "The solution file could not be opened by CodePass."));
        }
    }
}
