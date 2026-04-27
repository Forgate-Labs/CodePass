using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CodePass.Web.Services.CoverageAnalysis;

public sealed partial class CoberturaCoverageParser
{
    private static readonly StringComparer CoverageSortComparer = StringComparer.Ordinal;

    public CoverageAnalysisResult Parse(params string[] coberturaFilePaths)
    {
        ArgumentNullException.ThrowIfNull(coberturaFilePaths);

        var classTotals = new Dictionary<ClassCoverageKey, CoverageTotals>();

        foreach (var coberturaFilePath in coberturaFilePaths)
        {
            var document = XDocument.Load(coberturaFilePath);

            foreach (var packageElement in Descendants(document, "package"))
            {
                var projectName = (string?)packageElement.Attribute("name") ?? string.Empty;

                foreach (var classElement in Descendants(packageElement, "class"))
                {
                    var className = (string?)classElement.Attribute("name") ?? string.Empty;
                    var filePath = (string?)classElement.Attribute("filename") ?? string.Empty;
                    var key = new ClassCoverageKey(projectName, className, filePath);
                    var totals = classTotals.GetValueOrDefault(key);

                    foreach (var lineElement in Descendants(classElement, "line"))
                    {
                        totals.TotalLines++;

                        if (GetHits(lineElement) > 0)
                        {
                            totals.CoveredLines++;
                        }

                        var branchCoverage = ParseConditionCoverage((string?)lineElement.Attribute("condition-coverage"));
                        totals.CoveredBranches += branchCoverage.Covered;
                        totals.TotalBranches += branchCoverage.Total;
                    }

                    classTotals[key] = totals;
                }
            }
        }

        var classes = classTotals
            .OrderBy(row => row.Key.ProjectName, CoverageSortComparer)
            .ThenBy(row => row.Key.ClassName, CoverageSortComparer)
            .ThenBy(row => row.Key.FilePath, CoverageSortComparer)
            .Select(row => new CoverageClassCoverage(
                row.Key.ProjectName,
                row.Key.ClassName,
                row.Key.FilePath,
                row.Value.CoveredLines,
                row.Value.TotalLines,
                CalculatePercent(row.Value.CoveredLines, row.Value.TotalLines),
                row.Value.CoveredBranches,
                row.Value.TotalBranches,
                CalculatePercent(row.Value.CoveredBranches, row.Value.TotalBranches)))
            .ToArray();

        var projects = classes
            .GroupBy(row => row.ProjectName, CoverageSortComparer)
            .OrderBy(group => group.Key, CoverageSortComparer)
            .Select(group =>
            {
                var coveredLines = group.Sum(row => row.CoveredLines);
                var totalLines = group.Sum(row => row.TotalLines);
                var coveredBranches = group.Sum(row => row.CoveredBranches);
                var totalBranches = group.Sum(row => row.TotalBranches);

                return new CoverageProjectSummary(
                    group.Key,
                    coveredLines,
                    totalLines,
                    CalculatePercent(coveredLines, totalLines),
                    coveredBranches,
                    totalBranches,
                    CalculatePercent(coveredBranches, totalBranches));
            })
            .ToArray();

        return new CoverageAnalysisResult(projects, classes);
    }

    private static IEnumerable<XElement> Descendants(XContainer container, string localName)
    {
        return container.Descendants().Where(element => element.Name.LocalName == localName);
    }

    private static int GetHits(XElement lineElement)
    {
        var hits = (string?)lineElement.Attribute("hits");
        return int.TryParse(hits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static BranchCoverage ParseConditionCoverage(string? conditionCoverage)
    {
        if (string.IsNullOrWhiteSpace(conditionCoverage))
        {
            return new BranchCoverage(0, 0);
        }

        var match = ConditionCoverageFractionRegex().Match(conditionCoverage);
        if (!match.Success)
        {
            return new BranchCoverage(0, 0);
        }

        var covered = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var total = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return new BranchCoverage(covered, total);
    }

    private static double CalculatePercent(int covered, int total)
    {
        if (total == 0)
        {
            return 0;
        }

        return Math.Round(covered * 100d / total, 2, MidpointRounding.AwayFromZero);
    }

    [GeneratedRegex(@"\((\d+)\s*/\s*(\d+)\)")]
    private static partial Regex ConditionCoverageFractionRegex();

    private readonly record struct ClassCoverageKey(string ProjectName, string ClassName, string FilePath);

    private readonly record struct BranchCoverage(int Covered, int Total);

    private record struct CoverageTotals(
        int CoveredLines,
        int TotalLines,
        int CoveredBranches,
        int TotalBranches);
}
