using System.Text.Json;
using System.Text.RegularExpressions;
using CodePass.Web.Services.Rules;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodePass.Web.Services.RuleAnalysis;

public sealed class RoslynRuleAnalyzer : IRuleAnalyzer
{
    private static readonly object MSBuildRegistrationLock = new();
    private static readonly SymbolDisplayFormat SymbolDisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public async Task<IReadOnlyList<RuleAnalysisFinding>> AnalyzeAsync(
        string solutionPath,
        IReadOnlyList<AuthoredRuleDefinitionDto> rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count == 0)
        {
            return [];
        }

        RegisterMSBuildDefaults();

        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? Directory.GetCurrentDirectory();
        using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string>
        {
            ["DesignTimeBuild"] = "true",
            ["BuildProjectReferences"] = "false"
        });

        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        var findings = new List<RuleAnalysisFinding>();

        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var document in project.Documents.Where(document => document.SourceCodeKind == SourceCodeKind.Regular))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = document.FilePath;
                if (string.IsNullOrWhiteSpace(filePath) || !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = GetRelativePath(solutionDirectory, filePath);
                var applicableRules = rules
                    .Where(rule => IsRuleInScope(rule, relativePath))
                    .ToArray();

                if (applicableRules.Length == 0)
                {
                    continue;
                }

                var root = await document.GetSyntaxRootAsync(cancellationToken);
                if (root is null)
                {
                    continue;
                }

                SemanticModel? semanticModel = null;

                foreach (var rule in applicableRules)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    switch (rule.RuleKind.Trim().ToLowerInvariant())
                    {
                        case "syntax_presence":
                            findings.AddRange(AnalyzeSyntaxPresence(rule, root, relativePath));
                            break;
                        case "forbidden_api_usage":
                            semanticModel ??= await document.GetSemanticModelAsync(cancellationToken);
                            if (semanticModel is not null)
                            {
                                findings.AddRange(AnalyzeForbiddenApiUsage(rule, root, semanticModel, relativePath, cancellationToken));
                            }

                            break;
                        case "symbol_naming":
                            semanticModel ??= await document.GetSemanticModelAsync(cancellationToken);
                            if (semanticModel is not null)
                            {
                                findings.AddRange(AnalyzeSymbolNaming(rule, root, semanticModel, relativePath, cancellationToken));
                            }

                            break;
                    }
                }
            }
        }

        return findings;
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeSyntaxPresence(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        string relativePath)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var mode = GetString(parameters, "mode") ?? "forbid";
        var syntaxKinds = GetStringArray(parameters, "syntaxKinds").Select(value => value.Trim().ToLowerInvariant()).ToArray();
        if (syntaxKinds.Length == 0)
        {
            return [];
        }

        var findings = new List<RuleAnalysisFinding>();
        foreach (var syntaxKind in syntaxKinds)
        {
            var nodes = FindSyntaxNodes(root, syntaxKind).ToArray();
            if (string.Equals(mode, "require", StringComparison.OrdinalIgnoreCase))
            {
                if (nodes.Length == 0)
                {
                    findings.Add(CreateFinding(
                        rule,
                        $"Rule '{rule.Code}' requires syntax construct '{syntaxKind}'.",
                        relativePath,
                        root.GetLocation()));
                }

                continue;
            }

            findings.AddRange(nodes.Select(node => CreateFinding(
                rule,
                $"Rule '{rule.Code}' forbids syntax construct '{syntaxKind}'.",
                relativePath,
                node.GetLocation())));
        }

        return findings;
    }

    private static IEnumerable<SyntaxNode> FindSyntaxNodes(SyntaxNode root, string syntaxKind)
    {
        return syntaxKind switch
        {
            "var" => root.DescendantNodes()
                .OfType<VariableDeclarationSyntax>()
                .Where(declaration => declaration.Type.IsVar),
            "goto" => root.DescendantNodes().OfType<GotoStatementSyntax>(),
            "expression_bodied_member" => root.DescendantNodes()
                .OfType<ArrowExpressionClauseSyntax>()
                .Where(arrow => arrow.Parent is BaseMethodDeclarationSyntax
                    or PropertyDeclarationSyntax
                    or IndexerDeclarationSyntax
                    or OperatorDeclarationSyntax
                    or ConversionOperatorDeclarationSyntax),
            "missing_braces" => FindMissingBraceStatements(root),
            _ => []
        };
    }

    private static IEnumerable<SyntaxNode> FindMissingBraceStatements(SyntaxNode root)
    {
        foreach (var statement in root.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (statement.Statement is not BlockSyntax)
            {
                yield return statement.Statement;
            }

            if (statement.Else?.Statement is not null and not BlockSyntax and not IfStatementSyntax)
            {
                yield return statement.Else.Statement;
            }
        }

        foreach (var statement in root.DescendantNodes().OfType<ForStatementSyntax>().Where(statement => statement.Statement is not BlockSyntax))
        {
            yield return statement.Statement;
        }

        foreach (var statement in root.DescendantNodes().OfType<ForEachStatementSyntax>().Where(statement => statement.Statement is not BlockSyntax))
        {
            yield return statement.Statement;
        }

        foreach (var statement in root.DescendantNodes().OfType<WhileStatementSyntax>().Where(statement => statement.Statement is not BlockSyntax))
        {
            yield return statement.Statement;
        }

        foreach (var statement in root.DescendantNodes().OfType<DoStatementSyntax>().Where(statement => statement.Statement is not BlockSyntax))
        {
            yield return statement.Statement;
        }
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeForbiddenApiUsage(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        SemanticModel semanticModel,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var forbiddenSymbols = GetStringArray(parameters, "forbiddenSymbols").ToArray();
        if (forbiddenSymbols.Length == 0)
        {
            return [];
        }

        var alternatives = GetStringArray(parameters, "allowedAlternatives").ToArray();
        var findings = new List<RuleAnalysisFinding>();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol;
            var forbiddenSymbol = forbiddenSymbols.FirstOrDefault(forbidden => MatchesSymbol(symbol, forbidden));
            if (forbiddenSymbol is not null)
            {
                findings.Add(CreateFinding(rule, CreateForbiddenApiMessage(rule, forbiddenSymbol, alternatives), relativePath, invocation.GetLocation()));
            }
        }

        foreach (var objectCreation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(objectCreation.Type, cancellationToken).Symbol;
            var forbiddenSymbol = forbiddenSymbols.FirstOrDefault(forbidden => MatchesSymbol(symbol, forbidden));
            if (forbiddenSymbol is not null)
            {
                findings.Add(CreateFinding(rule, CreateForbiddenApiMessage(rule, forbiddenSymbol, alternatives), relativePath, objectCreation.GetLocation()));
            }
        }

        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (memberAccess.Parent is InvocationExpressionSyntax invocation && invocation.Expression == memberAccess)
            {
                continue;
            }

            var symbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
            var forbiddenSymbol = forbiddenSymbols.FirstOrDefault(forbidden => MatchesSymbol(symbol, forbidden));
            if (forbiddenSymbol is not null)
            {
                findings.Add(CreateFinding(rule, CreateForbiddenApiMessage(rule, forbiddenSymbol, alternatives), relativePath, memberAccess.GetLocation()));
            }
        }

        return findings;
    }

    private static IReadOnlyList<RuleAnalysisFinding> AnalyzeSymbolNaming(
        AuthoredRuleDefinitionDto rule,
        SyntaxNode root,
        SemanticModel semanticModel,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var parametersDocument = ParseRuleJsonDocument(rule, rule.ParametersJson, "parameters JSON");
        var parameters = parametersDocument.RootElement;
        var symbolKinds = GetStringArray(parameters, "symbolKinds").Select(value => value.Trim().ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (symbolKinds.Count == 0)
        {
            return [];
        }

        var accessibilities = GetStringArray(parameters, "accessibilities").Select(value => value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var capitalization = GetString(parameters, "capitalization");
        var requiredPrefix = GetString(parameters, "requiredPrefix") ?? string.Empty;
        var allowRegex = GetString(parameters, "allowRegex");
        var allowPattern = string.IsNullOrWhiteSpace(allowRegex) ? null : new Regex(allowRegex, RegexOptions.CultureInvariant);
        var findings = new List<RuleAnalysisFinding>();

        foreach (var candidate in EnumerateDeclaredSymbols(root, semanticModel, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!symbolKinds.Contains(candidate.Kind) || !AccessibilityMatches(candidate.Symbol, accessibilities))
            {
                continue;
            }

            if (NamePasses(candidate.Symbol.Name, requiredPrefix, capitalization, allowPattern))
            {
                continue;
            }

            findings.Add(CreateFinding(
                rule,
                $"Rule '{rule.Code}' requires {candidate.Kind} '{candidate.Symbol.Name}' to match the configured naming policy.",
                relativePath,
                candidate.Location));
        }

        return findings;
    }

    private static IEnumerable<DeclaredSymbolCandidate> EnumerateDeclaredSymbols(SyntaxNode root, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("class", symbol, declaration.Identifier.GetLocation());
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("interface", symbol, declaration.Identifier.GetLocation());
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("method", symbol, declaration.Identifier.GetLocation());
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("property", symbol, declaration.Identifier.GetLocation());
            }
        }

        foreach (var declaration in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declaration.Parent?.Parent is FieldDeclarationSyntax && semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is { } symbol)
            {
                yield return new DeclaredSymbolCandidate("field", symbol, declaration.Identifier.GetLocation());
            }
        }
    }

    private static bool NamePasses(string name, string requiredPrefix, string? capitalization, Regex? allowPattern)
    {
        if (allowPattern?.IsMatch(name) == true)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(requiredPrefix) && !name.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var nameWithoutPrefix = !string.IsNullOrEmpty(requiredPrefix) && name.StartsWith(requiredPrefix, StringComparison.Ordinal)
            ? name[requiredPrefix.Length..]
            : name;

        if (string.IsNullOrEmpty(nameWithoutPrefix) || string.IsNullOrWhiteSpace(capitalization))
        {
            return true;
        }

        return capitalization.Trim() switch
        {
            "PascalCase" => char.IsUpper(nameWithoutPrefix[0]),
            "camelCase" => char.IsLower(nameWithoutPrefix[0]),
            _ => true
        };
    }

    private static bool AccessibilityMatches(ISymbol symbol, IReadOnlySet<string> accessibilities)
    {
        return accessibilities.Count == 0 || accessibilities.Contains(symbol.DeclaredAccessibility.ToString().ToLowerInvariant());
    }

    private static bool MatchesSymbol(ISymbol? symbol, string forbiddenSymbol)
    {
        if (symbol is null || string.IsNullOrWhiteSpace(forbiddenSymbol))
        {
            return false;
        }

        var normalizedForbidden = NormalizeSymbolName(forbiddenSymbol);
        var candidates = new List<string>
        {
            NormalizeSymbolName(symbol.ToDisplayString(SymbolDisplayFormat)),
            NormalizeSymbolName(symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat))
        };

        if (symbol is IMethodSymbol methodSymbol)
        {
            candidates.Add(NormalizeSymbolName($"{methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat)}.{methodSymbol.Name}"));
        }
        else if (symbol.ContainingType is not null)
        {
            candidates.Add(NormalizeSymbolName($"{symbol.ContainingType.ToDisplayString(SymbolDisplayFormat)}.{symbol.Name}"));
        }

        return candidates.Any(candidate => string.Equals(candidate, normalizedForbidden, StringComparison.Ordinal)
            || candidate.StartsWith(normalizedForbidden + "(", StringComparison.Ordinal));
    }

    private static string NormalizeSymbolName(string symbolName)
    {
        const string globalPrefix = "global::";
        return symbolName.StartsWith(globalPrefix, StringComparison.Ordinal)
            ? symbolName[globalPrefix.Length..]
            : symbolName;
    }

    private static string CreateForbiddenApiMessage(AuthoredRuleDefinitionDto rule, string forbiddenSymbol, IReadOnlyList<string> alternatives)
    {
        var message = $"Rule '{rule.Code}' forbids API usage '{forbiddenSymbol}'.";
        return alternatives.Count == 0
            ? message
            : $"{message} Allowed alternatives: {string.Join(", ", alternatives)}.";
    }

    private static RuleAnalysisFinding CreateFinding(
        AuthoredRuleDefinitionDto rule,
        string message,
        string relativePath,
        Location location)
    {
        var lineSpan = location.GetLineSpan();
        return new RuleAnalysisFinding(
            RuleId: rule.Id,
            RuleCode: rule.Code,
            RuleTitle: rule.Title,
            RuleKind: rule.RuleKind,
            Severity: rule.Severity,
            Message: message,
            RelativeFilePath: relativePath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            StartColumn: lineSpan.StartLinePosition.Character + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1,
            EndColumn: lineSpan.EndLinePosition.Character + 1);
    }

    private static bool IsRuleInScope(AuthoredRuleDefinitionDto rule, string relativePath)
    {
        using var scopeDocument = ParseRuleJsonDocument(rule, rule.ScopeJson, "scope JSON");
        var scope = scopeDocument.RootElement;
        var files = GetStringArray(scope, "files").DefaultIfEmpty("**/*.cs").ToArray();
        var excludeFiles = GetStringArray(scope, "excludeFiles").ToArray();

        return files.Any(pattern => GlobMatches(pattern, relativePath))
            && !excludeFiles.Any(pattern => GlobMatches(pattern, relativePath));
    }

    private static bool GlobMatches(string pattern, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var normalizedPattern = NormalizePath(pattern.Trim());
        var normalizedPath = NormalizePath(relativePath);

        if (normalizedPattern == "*" || normalizedPattern == "**/*")
        {
            return true;
        }

        if (string.Equals(normalizedPattern, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!normalizedPattern.Contains('*', StringComparison.Ordinal))
        {
            return normalizedPath.EndsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase);
        }

        if (normalizedPattern == "**/*.cs")
        {
            return normalizedPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }

        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*\\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, filePath);
        return NormalizePath(relativePath);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static JsonDocument ParseRuleJsonDocument(AuthoredRuleDefinitionDto rule, string json, string payloadName)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Rule '{rule.Code}' {payloadName} is invalid.", exception);
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .OfType<string>()
            .ToArray();
    }

    private static void RegisterMSBuildDefaults()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (MSBuildRegistrationLock)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }

    private sealed record DeclaredSymbolCandidate(string Kind, ISymbol Symbol, Location Location);
}
