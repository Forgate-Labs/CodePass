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
                CreateField("targets", "Targets", "Closed target contexts for the syntax policy.", "array", true, Serialize(new[] { "local_declaration" }), ["local_declaration", "foreach_variable", "for_initializer", "using_declaration", "lambda_parameter", "field_declaration", "property_declaration", "method_declaration", "class_declaration", "record_declaration", "member_access"]),
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
            ]),
        new(
            Kind: "attribute_policy",
            Title: "Attribute policy",
            Description: "Require or forbid attributes on selected C# declarations.",
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
                CreateField("mode", "Mode", "Whether matching attributes are required or forbidden.", "string", true, Serialize("require"), ["require", "forbid"]),
                CreateField("targetKinds", "Target kinds", "Declaration kinds affected by the policy.", "array", true, Serialize(new[] { "class" }), ["class", "interface", "method", "property", "field"]),
                CreateField("attributes", "Attributes", "Attribute names to require or forbid.", "array", true, Serialize(new[] { "Authorize" })),
                CreateField("matchInherited", "Match inherited", "Whether inherited type attributes satisfy the policy.", "boolean", false, Serialize(false))
            ]),
        new(
            Kind: "dependency_policy",
            Title: "Dependency policy",
            Description: "Forbid dependencies on configured namespaces or types.",
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
                CreateField("sourceNamespaces", "Source namespaces", "Optional namespaces where the dependency policy applies.", "array", false, Serialize(Array.Empty<string>())),
                CreateField("forbiddenNamespaces", "Forbidden namespaces", "Namespaces that matching code cannot depend on.", "array", false, Serialize(new[] { "MyApp.Infrastructure" })),
                CreateField("forbiddenTypes", "Forbidden types", "Type names or fully qualified type names that matching code cannot depend on.", "array", false, Serialize(Array.Empty<string>()))
            ]),
        new(
            Kind: "method_metrics",
            Title: "Method metrics policy",
            Description: "Limit method size, parameter count, and cyclomatic complexity.",
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
                CreateField("maxLines", "Max lines", "Maximum method line count.", "number", false, Serialize(50)),
                CreateField("maxParameters", "Max parameters", "Maximum method parameter count.", "number", false, Serialize(5)),
                CreateField("maxCyclomaticComplexity", "Max cyclomatic complexity", "Maximum cyclomatic complexity.", "number", false, Serialize(10))
            ]),
        new(
            Kind: "class_metrics",
            Title: "Class metrics policy",
            Description: "Limit class size, method count, public surface, and constructor/field dependencies.",
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
                CreateField("maxLines", "Max lines", "Maximum class line count.", "number", false, Serialize(300)),
                CreateField("maxMethods", "Max methods", "Maximum methods declared directly in a class.", "number", false, Serialize(15)),
                CreateField("maxPublicMethods", "Max public methods", "Maximum public methods declared directly in a class.", "number", false, Serialize(10)),
                CreateField("maxDependencies", "Max dependencies", "Maximum distinct constructor and field dependency types.", "number", false, Serialize(5))
            ]),
        new(
            Kind: "interface_metrics",
            Title: "Interface metrics policy",
            Description: "Limit interface size and detect unimplemented member placeholders.",
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
                CreateField("maxMethods", "Max methods", "Maximum methods declared in an interface.", "number", false, Serialize(7)),
                CreateField("maxProperties", "Max properties", "Maximum properties declared in an interface.", "number", false, Serialize(5)),
                CreateField("forbidNotImplementedMembers", "Forbid not implemented members", "Whether members throwing NotImplementedException are violations.", "boolean", false, Serialize(false))
            ]),
        new(
            Kind: "inheritance_contract_policy",
            Title: "Inheritance contract policy",
            Description: "Detect risky inheritance patterns such as member hiding and unsupported overrides.",
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
                CreateField("forbidNotSupportedInOverrides", "Forbid unsupported overrides", "Whether overrides throwing NotSupportedException or NotImplementedException are violations.", "boolean", false, Serialize(true)),
                CreateField("forbidMemberHiding", "Forbid member hiding", "Whether members using the new modifier are violations.", "boolean", false, Serialize(true)),
                CreateField("requireNullableCompatibility", "Require nullable compatibility", "Whether override return nullability must not weaken the base contract.", "boolean", false, Serialize(true))
            ]),
        new(
            Kind: "polymorphism_opportunity",
            Title: "Polymorphism opportunity",
            Description: "Detect dispatch patterns that may be candidates for strategy or polymorphism.",
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
                CreateField("maxSwitchCases", "Max switch cases", "Maximum switch case labels before reporting a dispatch hotspot.", "number", false, Serialize(5)),
                CreateField("detectTypeChecks", "Detect type checks", "Whether is/as/pattern type checks are reported.", "boolean", false, Serialize(true)),
                CreateField("detectEnumDispatch", "Detect enum dispatch", "Whether switch statements over enum expressions are reported.", "boolean", false, Serialize(true))
            ]),
        new(
            Kind: "architecture_policy",
            Title: "Architecture policy",
            Description: "Restrict namespace dependencies for selected source namespaces.",
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
                CreateField("sourceNamespaces", "Source namespaces", "Optional namespaces where the architecture policy applies.", "array", false, Serialize(Array.Empty<string>())),
                CreateField("allowedNamespaces", "Allowed namespaces", "Optional namespace allowlist for dependencies.", "array", false, Serialize(Array.Empty<string>())),
                CreateField("forbiddenNamespaces", "Forbidden namespaces", "Namespaces that matching code cannot depend on.", "array", false, Serialize(Array.Empty<string>())),
                CreateField("allowSameNamespace", "Allow same namespace", "Whether dependencies inside the declaring namespace are allowed when an allowlist is configured.", "boolean", false, Serialize(true))
            ]),
        new(
            Kind: "dependency_inversion_policy",
            Title: "Dependency inversion policy",
            Description: "Detect dependencies on details and concrete constructor or field dependencies.",
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
                CreateField("sourceNamespaces", "Source namespaces", "Optional namespaces where the policy applies.", "array", false, Serialize(Array.Empty<string>())),
                CreateField("forbiddenNamespaces", "Forbidden namespaces", "Detail namespaces that matching code cannot depend on.", "array", false, Serialize(Array.Empty<string>())),
                CreateField("forbidConcreteDependencies", "Forbid concrete dependencies", "Whether constructor parameters and fields must depend on abstractions.", "boolean", false, Serialize(true)),
                CreateField("allowedAbstractionPrefixes", "Allowed abstraction prefixes", "Type name prefixes treated as abstractions in addition to interfaces and abstract types.", "array", false, Serialize(new[] { "I" }))
            ]),
        new(
            Kind: "exception_handling",
            Title: "Exception handling policy",
            Description: "Detect risky exception handling patterns.",
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
                CreateField("forbidEmptyCatch", "Forbid empty catch", "Whether empty catch blocks are violations.", "boolean", false, Serialize(true)),
                CreateField("forbidCatchAll", "Forbid catch-all", "Whether catch-all handlers are violations.", "boolean", false, Serialize(true)),
                CreateField("forbidThrowEx", "Forbid throw ex", "Whether throwing captured exception variables is a violation.", "boolean", false, Serialize(true)),
                CreateField("requireLogging", "Require logging", "Whether catch blocks must contain a logging call.", "boolean", false, Serialize(false))
            ]),
        new(
            Kind: "async_policy",
            Title: "Async policy",
            Description: "Enforce common async/await safety practices.",
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
                CreateField("forbidAsyncVoid", "Forbid async void", "Whether async void methods are violations.", "boolean", false, Serialize(true)),
                CreateField("requireCancellationToken", "Require CancellationToken", "Whether public async methods must accept a CancellationToken.", "boolean", false, Serialize(true)),
                CreateField("forbidBlockingCalls", "Forbid blocking calls", "Whether .Result, .Wait(), and GetResult() are violations.", "boolean", false, Serialize(true))
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
