using CodePass.Web.Services.Rules;
using FluentAssertions;

namespace CodePass.Web.Tests.Services;

public sealed class RuleCatalogServiceTests
{
    [Fact]
    public async Task GetRuleKindsAsync_ShouldReturnClosedSupportedCatalog()
    {
        var service = new RuleCatalogService();

        var catalog = await service.GetRuleKindsAsync();

        catalog.Select(rule => rule.Kind).Should().Equal("syntax_presence", "forbidden_api_usage", "symbol_naming");
        catalog.Should().OnlyContain(rule => rule.SchemaVersion == "1.0");
        catalog.Should().OnlyContain(rule => rule.Language == "csharp");
        catalog.Should().OnlyContain(rule => rule.ScopeFields.Count > 0);
        catalog.Should().OnlyContain(rule => rule.ParameterFields.Count > 0);
    }

    [Fact]
    public async Task GetRuleKindAsync_ShouldReturnNullForUnknownKind()
    {
        var service = new RuleCatalogService();

        var catalogEntry = await service.GetRuleKindAsync("not-supported");

        catalogEntry.Should().BeNull();
    }
}
