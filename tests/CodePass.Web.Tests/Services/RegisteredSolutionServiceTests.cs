using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Tests.Services;

public sealed class RegisteredSolutionServiceTests
{
    [Fact]
    public async Task CreateAndUpdate_ShouldRejectInvalidPaths()
    {
        using var tempDirectory = new TemporaryDirectory();
        await using var fixture = await RegisteredSolutionServiceFixture.CreateAsync();

        var createAction = async () => await fixture.Service.CreateAsync(new CreateRegisteredSolutionRequest("Invalid", Path.Combine(tempDirectory.Path, "invalid.txt")));

        await createAction.Should().ThrowAsync<InvalidOperationException>();

        var existing = await fixture.Service.CreateAsync(new CreateRegisteredSolutionRequest("Existing", tempDirectory.CreateFile("Existing.sln")));
        var updateAction = async () => await fixture.Service.UpdateAsync(existing.Id, new UpdateRegisteredSolutionRequest("Existing", Path.Combine(tempDirectory.Path, "missing.txt")));

        await updateAction.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnRecordsSortedByDisplayName()
    {
        using var tempDirectory = new TemporaryDirectory();
        await using var fixture = await RegisteredSolutionServiceFixture.CreateAsync();

        await fixture.Service.CreateAsync(new CreateRegisteredSolutionRequest("Zulu", tempDirectory.CreateFile("zulu.sln")));
        await fixture.Service.CreateAsync(new CreateRegisteredSolutionRequest("Alpha", tempDirectory.CreateFile("alpha.sln")));

        var results = await fixture.Service.GetAllAsync();

        results.Select(solution => solution.DisplayName).Should().ContainInOrder("Alpha", "Zulu");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveRegisteredSolution()
    {
        using var tempDirectory = new TemporaryDirectory();
        await using var fixture = await RegisteredSolutionServiceFixture.CreateAsync();
        var registeredSolution = await fixture.Service.CreateAsync(new CreateRegisteredSolutionRequest("Delete me", tempDirectory.CreateFile("DeleteMe.sln")));

        await fixture.Service.DeleteAsync(registeredSolution.Id);

        var results = await fixture.Service.GetAllAsync();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_ShouldDowngradeSavedSolutionStatusWhenFileDisappears()
    {
        using var tempDirectory = new TemporaryDirectory();
        await using var fixture = await RegisteredSolutionServiceFixture.CreateAsync();
        var solutionPath = tempDirectory.CreateFile("Refreshable.sln");
        var registeredSolution = await fixture.Service.CreateAsync(new CreateRegisteredSolutionRequest("Refreshable", solutionPath));

        File.Delete(solutionPath);

        var refreshed = await fixture.Service.RefreshAsync(registeredSolution.Id);

        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be(RegisteredSolutionStatus.FileNotFound);
        refreshed.LastValidatedAtUtc.Should().NotBeNull();
    }
}

internal sealed class RegisteredSolutionServiceFixture : IAsyncDisposable
{
    private RegisteredSolutionServiceFixture(CodePassDbContext dbContext, IRegisteredSolutionService service)
    {
        DbContext = dbContext;
        Service = service;
    }

    public CodePassDbContext DbContext { get; }

    public IRegisteredSolutionService Service { get; }

    public static async Task<RegisteredSolutionServiceFixture> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<CodePassDbContext>()
            .UseInMemoryDatabase($"codepass-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new CodePassDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var validator = new SolutionPathValidator();
        var service = new RegisteredSolutionService(dbContext, validator);

        return new RegisteredSolutionServiceFixture(dbContext, service);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }
}
