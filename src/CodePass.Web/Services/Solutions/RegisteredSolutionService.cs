using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Services.Solutions;

public sealed class RegisteredSolutionService(
    CodePassDbContext dbContext,
    ISolutionPathValidator solutionPathValidator) : IRegisteredSolutionService
{
    public async Task<IReadOnlyList<RegisteredSolution>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.RegisteredSolutions
            .AsNoTracking()
            .OrderBy(solution => solution.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<RegisteredSolution> CreateAsync(CreateRegisteredSolutionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await solutionPathValidator.ValidateAsync(request.SolutionPath, cancellationToken);
        EnsureValidForSave(validation);

        var utcNow = DateTimeOffset.UtcNow;
        var registeredSolution = new RegisteredSolution
        {
            Id = Guid.NewGuid(),
            DisplayName = NormalizeDisplayName(request.DisplayName),
            SolutionPath = validation.CanonicalPath!,
            Status = validation.Status,
            StatusMessage = validation.Message,
            LastValidatedAtUtc = utcNow,
            QualityScorePassThreshold = 80,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        dbContext.RegisteredSolutions.Add(registeredSolution);
        await dbContext.SaveChangesAsync(cancellationToken);

        return registeredSolution;
    }

    public async Task<RegisteredSolution> UpdateAsync(Guid id, UpdateRegisteredSolutionRequest request, CancellationToken cancellationToken = default)
    {
        var registeredSolution = await dbContext.RegisteredSolutions
            .SingleOrDefaultAsync(solution => solution.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Registered solution '{id}' was not found.");

        var validation = await solutionPathValidator.ValidateAsync(request.SolutionPath, cancellationToken);
        EnsureValidForSave(validation);

        registeredSolution.DisplayName = NormalizeDisplayName(request.DisplayName);
        registeredSolution.SolutionPath = validation.CanonicalPath!;
        registeredSolution.Status = validation.Status;
        registeredSolution.StatusMessage = validation.Message;
        registeredSolution.LastValidatedAtUtc = DateTimeOffset.UtcNow;
        registeredSolution.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return registeredSolution;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var registeredSolution = await dbContext.RegisteredSolutions
            .SingleOrDefaultAsync(solution => solution.Id == id, cancellationToken);

        if (registeredSolution is null)
        {
            return;
        }

        dbContext.RegisteredSolutions.Remove(registeredSolution);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RegisteredSolution?> RefreshAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var registeredSolution = await dbContext.RegisteredSolutions
            .SingleOrDefaultAsync(solution => solution.Id == id, cancellationToken);

        if (registeredSolution is null)
        {
            return null;
        }

        await RefreshSolutionAsync(registeredSolution, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return registeredSolution;
    }

    public async Task<int> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        var registeredSolutions = await dbContext.RegisteredSolutions.ToListAsync(cancellationToken);

        foreach (var registeredSolution in registeredSolutions)
        {
            await RefreshSolutionAsync(registeredSolution, cancellationToken);
        }

        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RefreshSolutionAsync(RegisteredSolution registeredSolution, CancellationToken cancellationToken)
    {
        var validation = await solutionPathValidator.ValidateAsync(registeredSolution.SolutionPath, cancellationToken);

        registeredSolution.SolutionPath = validation.CanonicalPath ?? registeredSolution.SolutionPath;
        registeredSolution.Status = validation.Status;
        registeredSolution.StatusMessage = validation.Message;
        registeredSolution.LastValidatedAtUtc = DateTimeOffset.UtcNow;
        registeredSolution.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A display name is required.", nameof(displayName));
        }

        return displayName.Trim();
    }

    private static void EnsureValidForSave(SolutionPathValidationResult validation)
    {
        if (validation.IsValid)
        {
            return;
        }

        throw new InvalidOperationException(validation.Message ?? "The solution path is invalid.");
    }
}
