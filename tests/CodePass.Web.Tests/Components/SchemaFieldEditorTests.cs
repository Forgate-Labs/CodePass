using System.Text.Json;
using Bunit;
using CodePass.Web.Components.Rules;
using CodePass.Web.Services.Rules;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace CodePass.Web.Tests.Components;

public sealed class SchemaFieldEditorTests : TestContext
{
    [Fact]
    public async Task NumberField_ShouldRenderAndEmitJsonNumber()
    {
        var definition = new RuleCatalogFieldDefinition(
            "maxLines",
            "Max lines",
            "Maximum method line count.",
            "number",
            IsRequired: false,
            JsonSerializer.SerializeToElement(50));
        var capturedValue = JsonSerializer.SerializeToElement(50);

        var cut = RenderComponent<SchemaFieldEditor>(parameters => parameters
            .Add(parameter => parameter.Definition, definition)
            .Add(parameter => parameter.Value, JsonSerializer.SerializeToElement(50))
            .Add(parameter => parameter.ValueChanged, EventCallback.Factory.Create<JsonElement?>(this, value => capturedValue = value!.Value)));

        cut.Find("[data-testid='number-field-maxLines']").GetAttribute("value").Should().Be("50");

        await cut.Find("[data-testid='number-field-maxLines']").ChangeAsync(new ChangeEventArgs { Value = "75" });

        capturedValue.ValueKind.Should().Be(JsonValueKind.Number);
        capturedValue.GetInt32().Should().Be(75);
    }
}
