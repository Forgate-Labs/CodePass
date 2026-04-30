namespace CodePass.Web.Services.Dashboard;

public interface IQualityScoreSettingsService
{
    Task<QualityScoreSettingsDto> GetSettingsAsync(Guid registeredSolutionId, CancellationToken cancellationToken = default);
    Task<QualityScoreSettingsDto> UpdateSettingsAsync(Guid registeredSolutionId, UpdateQualityScoreSettingsRequest request, CancellationToken cancellationToken = default);
}

public sealed record QualityScoreSettingsDto(Guid RegisteredSolutionId, double PassThreshold);

public sealed record UpdateQualityScoreSettingsRequest(double PassThreshold);
