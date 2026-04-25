using System.Text.Json;
using CodePass.Web.Data.Entities;

namespace CodePass.Web.Services.Rules;

public sealed record RuleCatalogFieldOption(string Value, string Label);

public sealed record RuleCatalogFieldDefinition(
    string Name,
    string Label,
    string Description,
    string JsonType,
    bool IsRequired,
    JsonElement? DefaultValue,
    IReadOnlyList<string>? AllowedValues = null,
    IReadOnlyList<RuleCatalogFieldOption>? Options = null);

public sealed record RuleKindCatalogEntry(
    string Kind,
    string Title,
    string Description,
    string SchemaVersion,
    string Language,
    string AnalysisLevel,
    IReadOnlyList<RuleCatalogFieldDefinition> ScopeFields,
    IReadOnlyList<RuleCatalogFieldDefinition> ParameterFields);

public sealed record SaveAuthoredRuleDefinitionRequest(
    string Code,
    string Title,
    string? Description,
    string RuleKind,
    string SchemaVersion,
    RuleSeverity Severity,
    string ScopeJson,
    string ParametersJson,
    bool IsEnabled);

public sealed record AuthoredRuleDefinitionDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    string RuleKind,
    string SchemaVersion,
    RuleSeverity Severity,
    string ScopeJson,
    string ParametersJson,
    string RawDefinitionJson,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
