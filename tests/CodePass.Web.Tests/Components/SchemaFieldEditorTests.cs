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
    public async Task ArrayFieldWithAllowedValues_ShouldRenderMultiSelectAndEmitJsonArray()
    {
        var definition = new RuleCatalogFieldDefinition(
            "targets",
            "Targets",
            "Closed target contexts for the syntax policy.",
            "array",
            IsRequired: true,
            JsonSerializer.SerializeToElement(new[] { "local_declaration" }),
            new[] { "local_declaration", "member_access" });
        var capturedValue = JsonSerializer.SerializeToElement(new[] { "local_declaration" });

        var cut = RenderComponent<SchemaFieldEditor>(parameters => parameters
            .Add(parameter => parameter.Definition, definition)
            .Add(parameter => parameter.Value, JsonSerializer.SerializeToElement(new[] { "local_declaration" }))
            .Add(parameter => parameter.ValueChanged, EventCallback.Factory.Create<JsonElement?>(this, value => capturedValue = value!.Value)));

        cut.Find("[data-testid='multi-select-field-targets']").HasAttribute("multiple").Should().BeTrue();
        cut.Find("[data-testid='multi-select-field-targets'] option[value='local_declaration']").HasAttribute("selected").Should().BeTrue();

        await cut.Find("[data-testid='multi-select-field-targets']").ChangeAsync(new ChangeEventArgs { Value = new[] { "member_access" } });

        capturedValue.ValueKind.Should().Be(JsonValueKind.Array);
        capturedValue.EnumerateArray().Select(item => item.GetString()).Should().Equal("member_access");
    }

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
