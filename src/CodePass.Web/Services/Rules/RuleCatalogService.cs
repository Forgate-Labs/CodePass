using System.Text.Json;

namespace CodePass.Web.Services.Rules;

public sealed class RuleCatalogService : IRuleCatalogService
{
    private static readonly IReadOnlyList<RuleKindCatalogEntry> RuleKinds =
    [
        new(
            Kind: "syntax_presence",
            Title: "Syntax presence policy",
            Description: "Require or forbid supported C# syntax constructs.",
            SchemaVersion: "1.0",
            Language: "csharp",
            AnalysisLevel: "syntax",
            ScopeFields:
            [
                CreateField("projects", "Projects", "Project glob patterns included by the rule.", "array", false, Serialize(new[] { "*" })),
                CreateField("files", "Files", "File glob patterns included by the rule.", "array", false, Serialize(new[] { "**/*.cs" })),
                CreateField("excludeFiles", "Exclude files", "File glob patterns excluded from the rule.", "array", false, Serialize(Array.Empty<string>()))
            ],
            ParameterFields:
            [
                CreateField("mode", "Mode", "Whether matching syntax is forbidden or required.", "string", true, Serialize("forbid"), ["forbid", "require"]),
                CreateField("targets", "Targets", "Closed target contexts for the syntax policy.", "array", true, Serialize(new[] { "local_declaration" })),
                CreateField("syntaxKinds", "Syntax kinds", "Supported syntax constructs to check.", "array", true, Serialize(new[] { "var" }), ["var", "goto", "expression_bodied_member", "missing_braces"]),
                CreateField("allowInTests", "Allow in tests", "Whether test projects can opt out.", "boolean", false, Serialize(false))
            ]),
        new(
            Kind: "forbidden_api_usage",
            Title: "Forbidden API usage",
            Description: "Disallow explicit APIs and suggest safer alternatives.",
            SchemaVersion: "1.0",
            Language: "csharp",
            AnalysisLevel: "semantic",
            ScopeFields:
            [
                CreateField("projects", "Projects", "Project glob patterns included by the rule.", "array", false, Serialize(new[] { "*" })),
                CreateField("files", "Files", "File glob patterns included by the rule.", "array", false, Serialize(new[] { "**/*.cs" })),
                CreateField("excludeFiles", "Exclude files", "File glob patterns excluded from the rule.", "array", false, Serialize(Array.Empty<string>()))
            ],
            ParameterFields:
            [
                CreateField("forbiddenSymbols", "Forbidden symbols", "Fully qualified symbol identifiers to block.", "array", true, Serialize(new[] { "System.Console.WriteLine" })),
                CreateField("allowedAlternatives", "Allowed alternatives", "Suggested replacement APIs.", "array", false, Serialize(Array.Empty<string>())),
                CreateField("allowInTests", "Allow in tests", "Whether test projects can opt out.", "boolean", false, Serialize(false))
            ]),
        new(
            Kind: "symbol_naming",
            Title: "Symbol naming policy",
            Description: "Enforce naming conventions for selected symbol kinds.",
            SchemaVersion: "1.0",
            Language: "csharp",
            AnalysisLevel: "semantic",
            ScopeFields:
            [
                CreateField("projects", "Projects", "Project glob patterns included by the rule.", "array", false, Serialize(new[] { "*" })),
                CreateField("files", "Files", "File glob patterns included by the rule.", "array", false, Serialize(new[] { "**/*.cs" })),
                CreateField("excludeFiles", "Exclude files", "File glob patterns excluded from the rule.", "array", false, Serialize(Array.Empty<string>()))
            ],
            ParameterFields:
            [
                CreateField("symbolKinds", "Symbol kinds", "Supported symbol kinds to validate.", "array", true, Serialize(new[] { "field" }), ["class", "interface", "method", "property", "field"]),
                CreateField("capitalization", "Capitalization", "Required capitalization strategy.", "string", true, Serialize("camelCase"), ["camelCase", "PascalCase"]),
                CreateField("requiredPrefix", "Required prefix", "Optional prefix that matching symbols must use.", "string", false, Serialize("_")),
                CreateField("allowRegex", "Allow regex", "Regex that can explicitly pass validation.", "string", false, Serialize(string.Empty))
            ])
    ];

    public Task<IReadOnlyList<RuleKindCatalogEntry>> GetRuleKindsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RuleKinds);
    }

    public Task<RuleKindCatalogEntry?> GetRuleKindAsync(string kind, CancellationToken cancellationToken = default)
    {
        var entry = RuleKinds.FirstOrDefault(ruleKind => string.Equals(ruleKind.Kind, kind?.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(entry);
    }

    private static RuleCatalogFieldDefinition CreateField(
        string name,
        string label,
        string description,
        string jsonType,
        bool isRequired,
        JsonElement? defaultValue,
        IReadOnlyList<string>? allowedValues = null)
    {
        return new RuleCatalogFieldDefinition(name, label, description, jsonType, isRequired, defaultValue, allowedValues);
    }

    private static JsonElement Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToElement(value);
    }
}
