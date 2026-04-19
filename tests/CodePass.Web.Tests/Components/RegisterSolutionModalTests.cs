using Bunit;
using CodePass.Web.Components.Solutions;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class RegisterSolutionModalTests : TestContext
{
    [Fact]
    public async Task Browse_ShouldAutoFillDisplayNameFromSelectedSolution()
    {
        JSInterop.Setup<RegisterSolutionModal.SolutionPickerResult?>("codePass.solutionPicker.pickSolution")
            .SetResult(new RegisterSolutionModal.SolutionPickerResult
            {
                FileName = "MySolution.sln",
                SuggestedPath = "/repo/MySolution.sln"
            });

        Services.AddSingleton<ISolutionPathValidator>(new FakeSolutionPathValidator(path => new SolutionPathValidationResult(CodePass.Web.Data.Entities.RegisteredSolutionStatus.Valid, path, "Valid")));
        Services.AddSingleton<IRegisteredSolutionService>(new FakeRegisteredSolutionService());

        var cut = RenderComponent<RegisterSolutionModal>(parameters => parameters.Add(parameter => parameter.IsOpen, true));

        await cut.Find("[data-testid='browse-button']").ClickAsync(new MouseEventArgs());

        cut.Find("[data-testid='display-name-input']").GetAttribute("value").Should().Be("MySolution");
        cut.Find("[data-testid='solution-path-input']").GetAttribute("value").Should().Be("/repo/MySolution.sln");
    }

    [Fact]
    public async Task Save_ShouldShowInlineErrorForInvalidSolutionPath()
    {
        Services.AddSingleton<ISolutionPathValidator>(new FakeSolutionPathValidator(_ => new SolutionPathValidationResult(CodePass.Web.Data.Entities.RegisteredSolutionStatus.Invalid, null, "Only direct .sln file paths are supported.")));
        Services.AddSingleton<IRegisteredSolutionService>(new FakeRegisteredSolutionService());

        var cut = RenderComponent<RegisterSolutionModal>(parameters => parameters.Add(parameter => parameter.IsOpen, true));

        await cut.Find("[data-testid='display-name-input']").ChangeAsync(new ChangeEventArgs { Value = "Broken" });
        await cut.Find("[data-testid='solution-path-input']").ChangeAsync(new ChangeEventArgs { Value = "/repo/Broken.txt" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Only direct .sln file paths are supported."));
    }

    [Fact]
    public async Task Save_ShouldRefreshListAfterSuccessfulRegistration()
    {
        var validator = new FakeSolutionPathValidator(path => new SolutionPathValidationResult(CodePass.Web.Data.Entities.RegisteredSolutionStatus.Valid, path, "Valid"));
        var service = new FakeRegisteredSolutionService();
        Services.AddSingleton<ISolutionPathValidator>(validator);
        Services.AddSingleton<IRegisteredSolutionService>(service);

        var registrationCount = 0;
        var cut = RenderComponent<RegisterSolutionModal>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true)
            .Add(parameter => parameter.OnRegistered, EventCallback.Factory.Create(this, () => registrationCount++)));

        await cut.Find("[data-testid='display-name-input']").ChangeAsync(new ChangeEventArgs { Value = "Repo" });
        await cut.Find("[data-testid='solution-path-input']").ChangeAsync(new ChangeEventArgs { Value = "/repo/Repo.sln" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            registrationCount.Should().Be(1);
            service.CreatedRequests.Should().ContainSingle();
            service.CreatedRequests[0].DisplayName.Should().Be("Repo");
            service.CreatedRequests[0].SolutionPath.Should().Be("/repo/Repo.sln");
        });
    }
}

internal sealed class FakeSolutionPathValidator(Func<string?, SolutionPathValidationResult> implementation) : ISolutionPathValidator
{
    public Task<SolutionPathValidationResult> ValidateAsync(string? solutionPath, CancellationToken cancellationToken = default)
        => Task.FromResult(implementation(solutionPath));
}
