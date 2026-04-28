namespace CodePass.Web.Services.Dashboard;

public interface IQualityScoreService
{
    Task<QualityScoreSnapshotDto> GetCurrentSnapshotAsync(
        Guid registeredSolutionId,
        CancellationToken cancellationToken = default);
}
