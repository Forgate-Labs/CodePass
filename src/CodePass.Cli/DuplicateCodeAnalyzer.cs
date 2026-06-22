using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

internal sealed class DuplicateCodeAnalyzer
{
    private const int ReportedBlockLimit = 50;
    private const int ReportedOccurrenceLimit = 10;
    private static readonly object MSBuildRegistrationLock = new();

    public async Task<DuplicateCodeAnalysisCliResult> AnalyzeAsync(
        string solutionPath,
        int minimumLineCount,
        int minimumTokenCount,
        CancellationToken cancellationToken = default,
        IProgress<DuplicateCodeAnalysisProgressDto>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        if (minimumLineCount <= 0) throw new ArgumentOutOfRangeException(nameof(minimumLineCount), "Minimum line count must be greater than zero.");
        if (minimumTokenCount <= 0) throw new ArgumentOutOfRangeException(nameof(minimumTokenCount), "Minimum token count must be greater than zero.");

        var startedAtUtc = DateTimeOffset.UtcNow;
        Report(progress, "Registering MSBuild defaults...", 15, null);
        RegisterMSBuildDefaults();

        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath)) ?? Directory.GetCurrentDirectory();
        using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string>
        {
            ["DesignTimeBuild"] = "true",
            ["BuildProjectReferences"] = "false"
        });

        Report(progress, "Opening solution with Roslyn/MSBuild...", 20, solutionPath);
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        var projects = solution.Projects.ToArray();
        var documents = projects
            .SelectMany(project => project.Documents)
            .Where(IsAnalyzableDocument)
            .GroupBy(document => Path.GetFullPath(document.FilePath!), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(document => document.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var documentCodes = new List<DocumentCode>(documents.Length);
        var processedDocuments = 0;
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processedDocuments++;
            var relativePath = GetRelativePath(solutionDirectory, document.FilePath!);
            Report(
                progress,
                $"Reading document {processedDocuments} of {documents.Length}...",
                CalculateReadPercent(processedDocuments, documents.Length),
                relativePath);

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root is null)
            {
                continue;
            }

            var lines = ExtractCodeLines(root, relativePath)
                .OrderBy(line => line.LineNumber)
                .ToArray();
            if (lines.Length > 0)
            {
                documentCodes.Add(new DocumentCode(relativePath, lines));
            }
        }

        Report(progress, "Finding duplicated code windows...", 82, null);
        var duplicateGroups = FindDuplicateGroups(documentCodes, minimumLineCount, minimumTokenCount);
        var duplicatedLineCount = CountDuplicatedLines(duplicateGroups);
        var totalLineCount = documentCodes.Sum(document => document.Lines.Count);
        var duplicatedCodePercent = totalLineCount <= 0
            ? 0
            : Math.Round(duplicatedLineCount * 100d / totalLineCount, 1, MidpointRounding.AwayFromZero);
        var reportedBlocks = duplicateGroups
            .OrderByDescending(group => group.Occurrences.Count)
            .ThenByDescending(group => group.TokenCount)
            .ThenBy(group => group.Occurrences[0].FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Occurrences[0].StartLine)
            .Take(ReportedBlockLimit)
            .Select(group => new DuplicateCodeBlockCliResult(
                group.Occurrences.Count,
                group.LineCount,
                group.TokenCount,
                group.Occurrences
                    .OrderBy(occurrence => occurrence.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(occurrence => occurrence.StartLine)
                    .Take(ReportedOccurrenceLimit)
                    .Select(occurrence => new DuplicateCodeOccurrenceCliResult(occurrence.FilePath, occurrence.StartLine, occurrence.EndLine))
                    .ToArray()))
            .ToArray();

        Report(progress, "Duplicate code analysis completed.", 100, null);
        return new DuplicateCodeAnalysisCliResult(
            AnalysisStatus.Succeeded,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            projects.Length,
            documentCodes.Count,
            totalLineCount,
            duplicatedLineCount,
            duplicatedCodePercent,
            duplicateGroups.Count,
            minimumLineCount,
            minimumTokenCount,
            null,
            reportedBlocks);
    }

    private static IReadOnlyList<DuplicateGroup> FindDuplicateGroups(
        IReadOnlyList<DocumentCode> documents,
        int minimumLineCount,
        int minimumTokenCount)
    {
        var windowsByText = new Dictionary<string, List<DuplicateWindowOccurrence>>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            if (document.Lines.Count < minimumLineCount)
            {
                continue;
            }

            for (var index = 0; index <= document.Lines.Count - minimumLineCount; index++)
            {
                var windowLines = document.Lines.Skip(index).Take(minimumLineCount).ToArray();
                var tokenCount = windowLines.Sum(line => line.TokenCount);
                if (tokenCount < minimumTokenCount)
                {
                    continue;
                }

                var normalizedText = string.Join('\n', windowLines.Select(line => line.NormalizedText));
                if (!windowsByText.TryGetValue(normalizedText, out var occurrences))
                {
                    occurrences = [];
                    windowsByText[normalizedText] = occurrences;
                }

                occurrences.Add(new DuplicateWindowOccurrence(
                    document.RelativePath,
                    windowLines[0].LineNumber,
                    windowLines[^1].LineNumber,
                    minimumLineCount,
                    tokenCount));
            }
        }

        return windowsByText.Values
            .Where(occurrences => occurrences.Count > 1)
            .Select(occurrences => new DuplicateGroup(
                occurrences[0].LineCount,
                occurrences[0].TokenCount,
                occurrences
                    .OrderBy(occurrence => occurrence.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(occurrence => occurrence.StartLine)
                    .ToArray()))
            .ToArray();
    }

    private static int CountDuplicatedLines(IReadOnlyList<DuplicateGroup> duplicateGroups)
    {
        var duplicatedLinesByFile = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in duplicateGroups)
        {
            foreach (var occurrence in group.Occurrences)
            {
                if (!duplicatedLinesByFile.TryGetValue(occurrence.FilePath, out var lines))
                {
                    lines = [];
                    duplicatedLinesByFile[occurrence.FilePath] = lines;
                }

                for (var line = occurrence.StartLine; line <= occurrence.EndLine; line++)
                {
                    lines.Add(line);
                }
            }
        }

        return duplicatedLinesByFile.Values.Sum(lines => lines.Count);
    }

    private static IReadOnlyList<CodeLine> ExtractCodeLines(SyntaxNode root, string relativePath)
    {
        var tokensByLine = new SortedDictionary<int, List<string>>();
        foreach (var token in root.DescendantTokens(descendIntoTrivia: false))
        {
            if (token.IsMissing || token.IsKind(SyntaxKind.EndOfFileToken))
            {
                continue;
            }

            var lineNumber = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            if (!tokensByLine.TryGetValue(lineNumber, out var tokens))
            {
                tokens = [];
                tokensByLine[lineNumber] = tokens;
            }

            tokens.Add(NormalizeToken(token));
        }

        return tokensByLine
            .Where(pair => pair.Value.Count > 0)
            .Select(pair => new CodeLine(relativePath, pair.Key, string.Join(' ', pair.Value), pair.Value.Count))
            .ToArray();
    }

    private static string NormalizeToken(SyntaxToken token)
    {
        return token.Kind() switch
        {
            SyntaxKind.NumericLiteralToken => "<number>",
            SyntaxKind.StringLiteralToken => "<string>",
            SyntaxKind.CharacterLiteralToken => "<char>",
            SyntaxKind.InterpolatedStringTextToken => "<string>",
            _ => token.Text
        };
    }

    private static bool IsAnalyzableDocument(Document document)
    {
        return document.SourceCodeKind == SourceCodeKind.Regular
            && !string.IsNullOrWhiteSpace(document.FilePath)
            && document.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            && !IsGeneratedOrBuildOutput(document.FilePath);
    }

    private static bool IsGeneratedOrBuildOutput(string filePath)
    {
        var normalizedPath = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var separator = Path.DirectorySeparatorChar;
        if (normalizedPath.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = Path.GetFileName(filePath);
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("GlobalUsings.g.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateReadPercent(int processedDocuments, int totalDocuments)
    {
        if (totalDocuments <= 0)
        {
            return 75;
        }

        return Math.Min(80, 25 + (int)Math.Round(processedDocuments * 55d / totalDocuments, MidpointRounding.AwayFromZero));
    }

    private static void Report(IProgress<DuplicateCodeAnalysisProgressDto>? progress, string message, int? percentComplete, string? detail)
    {
        progress?.Report(new DuplicateCodeAnalysisProgressDto(message, percentComplete, detail));
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, filePath);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
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

    private sealed record DocumentCode(string RelativePath, IReadOnlyList<CodeLine> Lines);
    private sealed record CodeLine(string FilePath, int LineNumber, string NormalizedText, int TokenCount);
    private sealed record DuplicateWindowOccurrence(string FilePath, int StartLine, int EndLine, int LineCount, int TokenCount);
    private sealed record DuplicateGroup(int LineCount, int TokenCount, IReadOnlyList<DuplicateWindowOccurrence> Occurrences);
}

internal sealed record DuplicateCodeAnalysisProgressDto(string Message, int? PercentComplete, string? Detail);
internal sealed record DuplicateCodeAnalysisCliResult(AnalysisStatus Status, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, int ProjectCount, int DocumentCount, int TotalLineCount, int DuplicatedLineCount, double DuplicatedCodePercent, int DuplicateBlockCount, int MinimumLineCount, int MinimumTokenCount, string? ErrorMessage, IReadOnlyList<DuplicateCodeBlockCliResult> Blocks);
internal sealed record DuplicateCodeBlockCliResult(int OccurrenceCount, int LineCount, int TokenCount, IReadOnlyList<DuplicateCodeOccurrenceCliResult> Occurrences);
internal sealed record DuplicateCodeOccurrenceCliResult(string FilePath, int StartLine, int EndLine);
