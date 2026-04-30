using CodePass.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Services.Dashboard;

public sealed class QualityScoreSettingsService(CodePassDbContext dbContext) : IQualityScoreSettingsService
{
    public const double DefaultPassThreshold = 80;

    public async Task<QualityScoreSettingsDto> GetSettingsAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default)
    {
        var solution = await dbContext.RegisteredSolutions
            .AsNoTracking()
            .SingleOrDefaultAsync(solution => solution.Id == registeredSolutionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Registered solution '{registeredSolutionId}' was not found.");

        return new QualityScoreSettingsDto(solution.Id, solution.QualityScorePassThreshold);
    }

    public async Task<QualityScoreSettingsDto> UpdateSettingsAsync(
        Guid registeredSolutionId,
        UpdateQualityScoreSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var passThreshold = NormalizePassThreshold(request.PassThreshold);
        var solution = await dbContext.RegisteredSolutions
            .SingleOrDefaultAsync(solution => solution.Id == registeredSolutionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Registered solution '{registeredSolutionId}' was not found.");

        solution.QualityScorePassThreshold = passThreshold;
        solution.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new QualityScoreSettingsDto(solution.Id, solution.QualityScorePassThreshold);
    }

    private static double NormalizePassThreshold(double passThreshold)
    {
        if (double.IsNaN(passThreshold) || double.IsInfinity(passThreshold) || passThreshold < 0 || passThreshold > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(passThreshold), passThreshold, "Pass threshold must be between 0 and 100.");
        }

        return Math.Round(passThreshold, 1, MidpointRounding.AwayFromZero);
    }
}
