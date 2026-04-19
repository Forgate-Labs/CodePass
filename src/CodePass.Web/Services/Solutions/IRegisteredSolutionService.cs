using CodePass.Web.Data.Entities;

namespace CodePass.Web.Services.Solutions;

public interface IRegisteredSolutionService
{
    Task<IReadOnlyList<RegisteredSolution>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RegisteredSolution> CreateAsync(CreateRegisteredSolutionRequest request, CancellationToken cancellationToken = default);
    Task<RegisteredSolution> UpdateAsync(Guid id, UpdateRegisteredSolutionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RegisteredSolution?> RefreshAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> RefreshAllAsync(CancellationToken cancellationToken = default);
}

public sealed record CreateRegisteredSolutionRequest(string DisplayName, string SolutionPath);

public sealed record UpdateRegisteredSolutionRequest(string DisplayName, string SolutionPath);
