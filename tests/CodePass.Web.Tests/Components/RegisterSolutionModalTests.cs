using Bunit;
using CodePass.Web.Components.Solutions;
using CodePass.Web.Data.Entities;
using CodePass.Web.Services.Solutions;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CodePass.Web.Tests.Components;

public sealed class RegisterSolutionModalTests : TestContext
{
    [Fact]
    public async Task ApplyPickerResult_ShouldRequireManualAbsolutePathWhenBrowserOnlyReturnsFileName()
    {
        var validator = new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid"));
        var service = new FakeRegisteredSolutionService();
        Services.AddSingleton<ISolutionPathValidator>(validator);
        Services.AddSingleton<IRegisteredSolutionService>(service);

        var cut = RenderComponent<RegisterSolutionModal>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true));

        await cut.InvokeAsync(() => cut.Instance.ApplyPickerResultAsync(new RegisterSolutionModal.SolutionPickerResult
        {
            FileName = "CodePass.sln",
            SuggestedPath = "CodePass.sln"
        }));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='display-name-input']").GetAttribute("value").Should().Be("CodePass");
            cut.Find("[data-testid='solution-path-input']").GetAttribute("value").Should().BeEmpty();
            cut.Find("[data-testid='picker-hint']").TextContent.Should().Contain("did not expose the real absolute local path");
        });

        await cut.Find("[data-testid='solution-path-input']").ChangeAsync(new ChangeEventArgs { Value = "CodePass.sln" });
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Paste the absolute local path to the .sln file before saving.");
            validator.Calls.Should().Be(0);
            service.CreatedRequests.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ApplyPickerResult_ShouldPopulateAbsolutePathWhenPickerProvidesOne()
    {
        var validator = new FakeSolutionPathValidator(path => new SolutionPathValidationResult(RegisteredSolutionStatus.Valid, path, "Valid"));
        var service = new FakeRegisteredSolutionService();
        Services.AddSingleton<ISolutionPathValidator>(validator);
        Services.AddSingleton<IRegisteredSolutionService>(service);

        var registeredCount = 0;
        var cut = RenderComponent<RegisterSolutionModal>(parameters => parameters
            .Add(parameter => parameter.IsOpen, true)
            .Add(parameter => parameter.OnRegistered, EventCallback.Factory.Create(this, () => registeredCount++)));

        await cut.InvokeAsync(() => cut.Instance.ApplyPickerResultAsync(new RegisterSolutionModal.SolutionPickerResult
        {
            FileName = "CodePass.sln",
            SuggestedPath = "/home/eduardo/Projects/CodePass/CodePass.sln"
        }));

        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='display-name-input']").GetAttribute("value").Should().BeEmpty();
            service.CreatedRequests.Should().ContainSingle();
            service.CreatedRequests[0].DisplayName.Should().Be("CodePass");
            service.CreatedRequests[0].SolutionPath.Should().Be("/home/eduardo/Projects/CodePass/CodePass.sln");
            registeredCount.Should().Be(1);
            validator.Calls.Should().Be(1);
        });
    }
}
