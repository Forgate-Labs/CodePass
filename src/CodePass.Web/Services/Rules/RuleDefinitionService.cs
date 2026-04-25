using System.Text.Json;
using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Services.Rules;

public sealed class RuleDefinitionService(
    CodePassDbContext dbContext,
    IRuleCatalogService ruleCatalogService) : IRuleDefinitionService
{
    private static readonly JsonSerializerOptions NormalizedJsonOptions = new() { WriteIndented = false };

    public async Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AuthoredRuleDefinitions
            .AsNoTracking()
            .OrderBy(rule => rule.Title)
            .ThenBy(rule => rule.Code)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<AuthoredRuleDefinitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.AuthoredRuleDefinitions
            .AsNoTracking()
            .Where(rule => rule.Id == id)
            .Select(MapToDtoExpression())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AuthoredRuleDefinitionDto> CreateAsync(SaveAuthoredRuleDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var validated = await ValidateAndNormalizeAsync(request, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;

        var entity = new AuthoredRuleDefinition
        {
            Id = Guid.NewGuid(),
            Code = validated.Code,
            Title = validated.Title,
            Description = validated.Description,
            RuleKind = validated.RuleKind.Kind,
            SchemaVersion = validated.RuleKind.SchemaVersion,
            Severity = validated.Severity,
            ScopeJson = validated.ScopeJson,
            ParametersJson = validated.ParametersJson,
            RawDefinitionJson = validated.RawDefinitionJson,
            IsEnabled = validated.IsEnabled,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        dbContext.AuthoredRuleDefinitions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<AuthoredRuleDefinitionDto> UpdateAsync(Guid id, SaveAuthoredRuleDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AuthoredRuleDefinitions.SingleOrDefaultAsync(rule => rule.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Authored rule '{id}' was not found.");

        var validated = await ValidateAndNormalizeAsync(request, cancellationToken);

        entity.Code = validated.Code;
        entity.Title = validated.Title;
        entity.Description = validated.Description;
        entity.RuleKind = validated.RuleKind.Kind;
        entity.SchemaVersion = validated.RuleKind.SchemaVersion;
        entity.Severity = validated.Severity;
        entity.ScopeJson = validated.ScopeJson;
        entity.ParametersJson = validated.ParametersJson;
        entity.RawDefinitionJson = validated.RawDefinitionJson;
        entity.IsEnabled = validated.IsEnabled;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AuthoredRuleDefinitions.SingleOrDefaultAsync(rule => rule.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.AuthoredRuleDefinitions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuthoredRuleDefinitionDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AuthoredRuleDefinitions
            .AsNoTracking()
            .Where(rule => rule.IsEnabled)
            .OrderBy(rule => rule.Title)
            .ThenBy(rule => rule.Code)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    private async Task<ValidatedRuleDefinition> ValidateAndNormalizeAsync(SaveAuthoredRuleDefinitionRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RawDefinitionJson))
        {
            return await ValidateRawDefinitionAsync(request, cancellationToken);
        }

        if (!Enum.IsDefined(request.Severity))
        {
            throw new InvalidOperationException($"Unsupported severity '{request.Severity}'.");
        }

        var ruleKind = await ruleCatalogService.GetRuleKindAsync(request.RuleKind, cancellationToken)
            ?? throw new InvalidOperationException($"Rule kind '{request.RuleKind}' is not supported.");

        if (!string.Equals(ruleKind.SchemaVersion, request.SchemaVersion?.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Rule kind '{ruleKind.Kind}' only supports schema version '{ruleKind.SchemaVersion}'.");
        }

        var scope = ParseObject(request.ScopeJson, nameof(request.ScopeJson));
        var parameters = ParseObject(request.ParametersJson, nameof(request.ParametersJson));

        ValidateFields(scope, ruleKind.ScopeFields, "scope");
        ValidateFields(parameters, ruleKind.ParameterFields, "parameters");

        var code = NormalizeRequired(request.Code, nameof(request.Code));
        var title = NormalizeRequired(request.Title, nameof(request.Title));
        var description = NormalizeOptional(request.Description);

        return new ValidatedRuleDefinition(
            ruleKind,
            code,
            title,
            description,
            request.Severity,
            request.IsEnabled,
            scope,
            parameters,
            SerializeNormalized(scope),
            SerializeNormalized(parameters),
            BuildRawDefinitionJson(code, title, description, ruleKind.Kind, ruleKind.SchemaVersion, request.Severity, request.IsEnabled, scope, parameters));
    }

    private async Task<ValidatedRuleDefinition> ValidateRawDefinitionAsync(SaveAuthoredRuleDefinitionRequest request, CancellationToken cancellationToken)
    {
        var document = ParseObject(request.RawDefinitionJson!, nameof(request.RawDefinitionJson));
        var code = GetRequiredString(document, "id");
        var title = GetRequiredString(document, "title");
        var description = GetOptionalString(document, "description");
        var kind = GetRequiredString(document, "kind");
        var schemaVersion = GetRequiredString(document, "schemaVersion");
        var severity = ParseSeverity(GetRequiredString(document, "severity"));
        var isEnabled = GetRequiredBoolean(document, "enabled");
        var language = GetRequiredString(document, "language");

        if (!string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Raw rule JSON must use language 'csharp'.");
        }

        var ruleKind = await ruleCatalogService.GetRuleKindAsync(kind, cancellationToken)
            ?? throw new InvalidOperationException($"Rule kind '{kind}' is not supported.");

        if (!string.Equals(ruleKind.SchemaVersion, schemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Rule kind '{ruleKind.Kind}' only supports schema version '{ruleKind.SchemaVersion}'.");
        }

        var scope = GetRequiredObject(document, "scope");
        var parameters = GetRequiredObject(document, "parameters");

        ValidateFields(scope, ruleKind.ScopeFields, "scope");
        ValidateFields(parameters, ruleKind.ParameterFields, "parameters");

        return new ValidatedRuleDefinition(
            ruleKind,
            code,
            title,
            description,
            severity,
            isEnabled,
            scope,
            parameters,
            SerializeNormalized(scope),
            SerializeNormalized(parameters),
            BuildRawDefinitionJson(code, title, description, ruleKind.Kind, ruleKind.SchemaVersion, severity, isEnabled, scope, parameters));
    }

    private static JsonElement ParseObject(string json, string paramName)
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"{paramName} must be a JSON object.");
            }

            return element;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{paramName} must contain valid JSON.", ex);
        }
    }

    private static void ValidateFields(JsonElement document, IReadOnlyList<RuleCatalogFieldDefinition> fieldDefinitions, string sectionName)
    {
        var properties = document.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);

        foreach (var requiredField in fieldDefinitions.Where(field => field.IsRequired))
        {
            if (!properties.ContainsKey(requiredField.Name))
            {
                throw new InvalidOperationException($"The {sectionName} JSON is missing required field '{requiredField.Name}'.");
            }
        }

        foreach (var property in properties)
        {
            var definition = fieldDefinitions.FirstOrDefault(field => field.Name == property.Key)
                ?? throw new InvalidOperationException($"The {sectionName} JSON contains unsupported field '{property.Key}'.");

            EnsureJsonType(property.Value, definition, sectionName);
            EnsureAllowedValues(property.Value, definition, sectionName);
        }
    }

    private static void EnsureJsonType(JsonElement value, RuleCatalogFieldDefinition definition, string sectionName)
    {
        var matchesType = definition.JsonType switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "array" => value.ValueKind == JsonValueKind.Array,
            "object" => value.ValueKind == JsonValueKind.Object,
            _ => false
        };

        if (!matchesType)
        {
            throw new InvalidOperationException($"Field '{definition.Name}' in {sectionName} must be a JSON {definition.JsonType}.");
        }
    }

    private static void EnsureAllowedValues(JsonElement value, RuleCatalogFieldDefinition definition, string sectionName)
    {
        if (definition.AllowedValues is null || definition.AllowedValues.Count == 0)
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var stringValue = value.GetString();
            if (!definition.AllowedValues.Contains(stringValue!, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Field '{definition.Name}' in {sectionName} has unsupported value '{stringValue}'.");
            }

            return;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException($"Field '{definition.Name}' in {sectionName} must contain only strings.");
                }

                var itemValue = item.GetString();
                if (!definition.AllowedValues.Contains(itemValue!, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException($"Field '{definition.Name}' in {sectionName} has unsupported value '{itemValue}'.");
                }
            }
        }
    }

    private static string GetRequiredString(JsonElement document, string propertyName)
    {
        if (!document.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Raw rule JSON is missing required string property '{propertyName}'.");
        }

        return NormalizeRequired(property.GetString() ?? string.Empty, propertyName);
    }

    private static string? GetOptionalString(JsonElement document, string propertyName)
    {
        if (!document.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Raw rule JSON property '{propertyName}' must be a string or null.");
        }

        return NormalizeOptional(property.GetString());
    }

    private static bool GetRequiredBoolean(JsonElement document, string propertyName)
    {
        if (!document.TryGetProperty(propertyName, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidOperationException($"Raw rule JSON is missing required boolean property '{propertyName}'.");
        }

        return property.GetBoolean();
    }

    private static JsonElement GetRequiredObject(JsonElement document, string propertyName)
    {
        if (!document.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Raw rule JSON is missing required object property '{propertyName}'.");
        }

        return property;
    }

    private static RuleSeverity ParseSeverity(string severity)
    {
        if (!Enum.TryParse<RuleSeverity>(severity, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException($"Unsupported severity '{severity}'.");
        }

        return parsed;
    }

    private static string BuildRawDefinitionJson(
        string code,
        string title,
        string? description,
        string ruleKind,
        string schemaVersion,
        RuleSeverity severity,
        bool isEnabled,
        JsonElement scope,
        JsonElement parameters)
    {
        var document = new
        {
            id = code,
            title,
            description,
            kind = ruleKind,
            schemaVersion,
            severity = severity.ToString().ToLowerInvariant(),
            enabled = isEnabled,
            language = "csharp",
            scope,
            parameters
        };

        return JsonSerializer.Serialize(document, NormalizedJsonOptions);
    }

    private static string SerializeNormalized(JsonElement element)
    {
        return JsonSerializer.Serialize(element, NormalizedJsonOptions);
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AuthoredRuleDefinitionDto MapToDto(AuthoredRuleDefinition entity)
    {
        return new AuthoredRuleDefinitionDto(
            entity.Id,
            entity.Code,
            entity.Title,
            entity.Description,
            entity.RuleKind,
            entity.SchemaVersion,
            entity.Severity,
            entity.ScopeJson,
            entity.ParametersJson,
            entity.RawDefinitionJson,
            entity.IsEnabled,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

    private static System.Linq.Expressions.Expression<Func<AuthoredRuleDefinition, AuthoredRuleDefinitionDto>> MapToDtoExpression()
    {
        return entity => new AuthoredRuleDefinitionDto(
            entity.Id,
            entity.Code,
            entity.Title,
            entity.Description,
            entity.RuleKind,
            entity.SchemaVersion,
            entity.Severity,
            entity.ScopeJson,
            entity.ParametersJson,
            entity.RawDefinitionJson,
            entity.IsEnabled,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

    private sealed record ValidatedRuleDefinition(
        RuleKindCatalogEntry RuleKind,
        string Code,
        string Title,
        string? Description,
        RuleSeverity Severity,
        bool IsEnabled,
        JsonElement Scope,
        JsonElement Parameters,
        string ScopeJson,
        string ParametersJson,
        string RawDefinitionJson);
}
