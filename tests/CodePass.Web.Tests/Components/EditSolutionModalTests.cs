using Bunit;
using CodePass.Web.Components.Solutions;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class EditSolutionModalTests : TestContext
{
    [Fact]
    public async Task Save_ShouldUpdateOnlyDisplayNameWithoutRevalidatingSamePath()
    {
        var validator = new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid"));
        var service = new FakeRegisteredSolutionService(CreateSolution("Alpha", "/repo/Alpha.sln", RegisteredSolutionStatus.Valid));
        Services.AddSingleton<ISolutionPathValidator>(validator);
        Services.AddSingleton<IRegisteredSolutionService>(service);

        var saveCount = 0;
        var solution = (await service.GetAllAsync()).Single();
        var cut = RenderComponent<EditSolutionModal>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true)
            .Add(parameter => parameter.Solution, solution)
            .Add(parameter => parameter.OnSaved, EventCallback.Factory.Create(this, () => saveCount++)));

        await cut.Find("[data-testid='edit-display-name-input']").ChangeAsync(new ChangeEventArgs { Value = "Beta" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            saveCount.Should().Be(1);
            service.UpdatedRequests.Should().ContainSingle();
            service.UpdatedRequests[0].Request.DisplayName.Should().Be("Beta");
            service.UpdatedRequests[0].Request.SolutionPath.Should().Be("/repo/Alpha.sln");
            validator.Calls.Should().Be(0);
        });
    }

    [Fact]
    public async Task Save_ShouldValidateAndUpdateWhenPathChangesToValidSolution()
    {
        var validator = new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, $"/canonical{path}", "Valid"));
        var service = new FakeRegisteredSolutionService(CreateSolution("Alpha", "/repo/Alpha.sln", RegisteredSolutionStatus.Valid));
        Services.AddSingleton<ISolutionPathValidator>(validator);
        Services.AddSingleton<IRegisteredSolutionService>(service);

        var saveCount = 0;
        var solution = (await service.GetAllAsync()).Single();
        var cut = RenderComponent<EditSolutionModal>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true)
            .Add(parameter => parameter.Solution, solution)
            .Add(parameter => parameter.OnSaved, EventCallback.Factory.Create(this, () => saveCount++)));

        await cut.Find("[data-testid='edit-solution-path-input']").ChangeAsync(new ChangeEventArgs { Value = "/replacement/Beta.sln" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            saveCount.Should().Be(1);
            validator.Calls.Should().Be(1);
            service.UpdatedRequests.Should().ContainSingle();
            service.UpdatedRequests[0].Request.SolutionPath.Should().Be("/canonical/replacement/Beta.sln");
        });
    }

    [Theory]
    [InlineData(RegisteredSolutionStatus.Invalid, "Only direct .sln file paths are supported.")]
    [InlineData(RegisteredSolutionStatus.FileNotFound, "The solution file does not exist.")]
    [InlineData(RegisteredSolutionStatus.PathInaccessible, "CodePass cannot access that path.")]
    public async Task Save_ShouldBlockWhenChangedPathIsInvalid(RegisteredSolutionStatus status, string message)
    {
        var validator = new FakeSolutionPathValidator(_ => new SolutionPathValidationResult(status, null, message));
        var service = new FakeRegisteredSolutionService(CreateSolution("Alpha", "/repo/Alpha.sln", RegisteredSolutionStatus.Valid));
        Services.AddSingleton<ISolutionPathValidator>(validator);
        Services.AddSingleton<IRegisteredSolutionService>(service);

        var solution = (await service.GetAllAsync()).Single();
        var cut = RenderComponent<EditSolutionModal>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true)
            .Add(parameter => parameter.Solution, solution));

        await cut.Find("[data-testid='edit-solution-path-input']").ChangeAsync(new ChangeEventArgs { Value = "/broken/Alpha.sln" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain(message);
            service.UpdatedRequests.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task Delete_ShouldStayHiddenUntilConfirmationAndThenRemoveSolution()
    {
        var validator = new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid"));
        var service = new FakeRegisteredSolutionService(CreateSolution("Alpha", "/repo/Alpha.sln", RegisteredSolutionStatus.Valid));
        Services.AddSingleton<ISolutionPathValidator>(validator);
        Services.AddSingleton<IRegisteredSolutionService>(service);

        var deleteCount = 0;
        var solution = (await service.GetAllAsync()).Single();
        var cut = RenderComponent<EditSolutionModal>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true)
            .Add(parameter => parameter.Solution, solution)
            .Add(parameter => parameter.OnDeleted, EventCallback.Factory.Create(this, () => deleteCount++)));

        cut.FindAll("[data-testid='confirm-delete-button']").Should().BeEmpty();

        await cut.Find("[data-testid='show-delete-confirmation-button']").ClickAsync(new MouseEventArgs());
        cut.FindAll("[data-testid='confirm-delete-button']").Should().ContainSingle();

        await cut.Find("[data-testid='confirm-delete-button']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            deleteCount.Should().Be(1);
            service.DeletedIds.Should().ContainSingle(id => id == solution.Id);
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

internal sealed class FakeSolutionPathValidator(Func<string?, SolutionPathValidationResult> implementation) : ISolutionPathValidator
{
    public int Calls { get; private set; }

    public Task<SolutionPathValidationResult> ValidateAsync(string? solutionPath, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(implementation(solutionPath));
    }
}
