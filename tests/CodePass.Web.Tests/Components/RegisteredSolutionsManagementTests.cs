using Bunit;
using CodePass.Web.Components.Pages;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class RegisteredSolutionsManagementTests : TestContext
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
    public void OpenWorkspace_ShouldNavigateToSolutionScoreDashboard()
    {
        var solution = CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var service = new FakeRegisteredSolutionService(solution);
        Services.AddSingleton<IRegisteredSolutionService>(service);
        Services.AddSingleton<ISolutionPathValidator>(new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid")));

        var cut = RenderComponent<RegisteredSolutions>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='open-solution-workspace-link']")
                .GetAttribute("href")
                .Should()
                .Be($"/solutions/{solution.Id}/dashboard");
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

    [Fact]
    public async Task ManageSave_ShouldRefreshCardListAfterSuccessfulUpdate()
    {
        var service = new FakeRegisteredSolutionService(
            CreateSolution("Zulu", "/solutions/zulu.sln", RegisteredSolutionStatus.Valid),
            CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid));
        Services.AddSingleton<IRegisteredSolutionService>(service);
        Services.AddSingleton<ISolutionPathValidator>(new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, $"/canonical{path}", "Valid")));

        var cut = RenderComponent<RegisteredSolutions>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='manage-solution-button']").Should().HaveCount(2));

        await cut.FindAll("[data-testid='manage-solution-button']")[0].ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='edit-display-name-input']").ChangeAsync(new ChangeEventArgs { Value = "Omega" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='solution-name']").Select(element => element.TextContent.Trim())
                .Should()
                .ContainInOrder("Omega", "Zulu");
            service.UpdatedRequests.Should().ContainSingle();
        });
    }

    [Fact]
    public async Task ManageDelete_ShouldRefreshCardListAfterSuccessfulRemoval()
    {
        var alpha = CreateSolution("Alpha", "/solutions/alpha.sln", RegisteredSolutionStatus.Valid);
        var zulu = CreateSolution("Zulu", "/solutions/zulu.sln", RegisteredSolutionStatus.Valid);
        var service = new FakeRegisteredSolutionService(alpha, zulu);
        Services.AddSingleton<IRegisteredSolutionService>(service);
        Services.AddSingleton<ISolutionPathValidator>(new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid")));

        var cut = RenderComponent<RegisteredSolutions>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='manage-solution-button']").Should().HaveCount(2));

        await cut.FindAll("[data-testid='manage-solution-button']")[0].ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='show-delete-confirmation-button']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='confirm-delete-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='solution-name']").Select(element => element.TextContent.Trim())
                .Should()
                .ContainSingle().Which.Should().Be("Zulu");
            service.DeletedIds.Should().ContainSingle(id => id == alpha.Id);
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
    public List<(Guid Id, UpdateRegisteredSolutionRequest Request)> UpdatedRequests { get; } = [];
    public List<Guid> DeletedIds { get; } = [];

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
    {
        UpdatedRequests.Add((id, request));

        var solution = _solutions.Single(item => item.Id == id);
        solution.DisplayName = request.DisplayName;
        solution.SolutionPath = request.SolutionPath;
        solution.Status = RegisteredSolutionStatus.Valid;
        solution.StatusMessage = "Valid";
        solution.LastValidatedAtUtc = DateTimeOffset.UtcNow;
        solution.UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Task.FromResult(solution);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeletedIds.Add(id);
        _solutions.RemoveAll(solution => solution.Id == id);
        return Task.CompletedTask;
    }

    public Task<RegisteredSolution?> RefreshAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_solutions.SingleOrDefault(solution => solution.Id == id));

    public Task<int> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        RefreshAllCalls++;
        return Task.FromResult(_solutions.Count);
    }
}
