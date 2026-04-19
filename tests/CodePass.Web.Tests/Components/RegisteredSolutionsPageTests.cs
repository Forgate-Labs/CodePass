using Bunit;
using CodePass.Web.Components.Pages;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class RegisteredSolutionsPageTests : TestContext
{
    [Fact]
    public void EmptyState_ShouldRenderWhenNoSolutionsExist()
    {
        Services.AddSingleton<IRegisteredSolutionService>(new FakeRegisteredSolutionService());
        Services.AddSingleton<ISolutionPathValidator>(new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid")));

        var cut = RenderComponent<RegisteredSolutions>();

        cut.Markup.Should().Contain("No registered solutions yet");
    }

    [Fact]
    public void Cards_ShouldRenderOrderedByDisplayName()
    {
        var service = new FakeRegisteredSolutionService(
            CreateSolution("Zulu", "/solutions/zulu.sln", RegisteredSolutionStatus.Valid),
            CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.FileNotFound));
        Services.AddSingleton<IRegisteredSolutionService>(service);
        Services.AddSingleton<ISolutionPathValidator>(new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid")));

        var cut = RenderComponent<RegisteredSolutions>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='solution-name']").Select(element => element.TextContent.Trim())
                .Should()
                .ContainInOrder("Alpha", "Zulu");
        });
    }

    [Fact]
    public void StatusBadge_ShouldRenderHumanReadableStatusText()
    {
        var service = new FakeRegisteredSolutionService(
            CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.PathInaccessible, "The solution file could not be opened by CodePass."));
        Services.AddSingleton<IRegisteredSolutionService>(service);
        Services.AddSingleton<ISolutionPathValidator>(new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid")));

        var cut = RenderComponent<RegisteredSolutions>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='solution-status']").TextContent.Trim().Should().Be("Path inaccessible");
            cut.Markup.Should().Contain("The solution file could not be opened by CodePass.");
        });
    }

    private static RegisteredSolution CreateSolution(string displayName, string path, RegisteredSolutionStatus status, string? statusMessage = null)
        => new()
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            SolutionPath = path,
            Status = status,
            StatusMessage = statusMessage,
            LastValidatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
}

internal sealed class FakeRegisteredSolutionService(params RegisteredSolution[] seededSolutions) : IRegisteredSolutionService
{
    private readonly List<RegisteredSolution> _solutions = seededSolutions.OrderBy(solution => solution.DisplayName).ToList();

    public int RefreshAllCalls { get; private set; }

    public List<CreateRegisteredSolutionRequest> CreatedRequests { get; } = [];

    public Task<IReadOnlyList<RegisteredSolution>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RegisteredSolution>>(_solutions.OrderBy(solution => solution.DisplayName).ToList());

    public Task<RegisteredSolution> CreateAsync(CreateRegisteredSolutionRequest request, CancellationToken cancellationToken = default)
    {
        CreatedRequests.Add(request);

        var solution = new RegisteredSolution
        {
            Id = Guid.NewGuid(),
            DisplayName = request.DisplayName,
            SolutionPath = request.SolutionPath,
            Status = RegisteredSolutionStatus.Valid,
            StatusMessage = "The solution file is accessible.",
            LastValidatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        _solutions.Add(solution);
        return Task.FromResult(solution);
    }

    public Task<RegisteredSolution> UpdateAsync(Guid id, UpdateRegisteredSolutionRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<RegisteredSolution?> RefreshAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        RefreshAllCalls++;
        return Task.FromResult(_solutions.Count);
    }
}
