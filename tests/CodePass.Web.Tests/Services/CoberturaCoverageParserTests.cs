using CodePass.Web.Services.CoverageAnalysis;
using FluentAssertions;

namespace CodePass.Web.Tests.Services;

public sealed class CoberturaCoverageParserTests
{
    [Fact]
    public async Task Parse_ShouldNormalizePackagesClassesLinesBranchesAndRoundedPercentages()
    {
        using var fixture = new CoberturaXmlFixture();
        var coveragePath = await fixture.WriteAsync(
            "coverage.cobertura.xml",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0" branch-rate="0" version="1.0">
              <packages>
                <package name="Sample.Project" line-rate="0" branch-rate="0">
                  <classes>
                    <class name="Sample.Project.Foo" filename="Foo.cs" line-rate="0" branch-rate="0">
                      <lines>
                        <line number="10" hits="1" branch="false" />
                        <line number="11" hits="0" branch="true" condition-coverage="50% (1/2)" />
                        <line number="12" hits="2" branch="false" />
                      </lines>
                    </class>
                    <class name="Sample.Project.Bar" filename="Bar.cs" line-rate="0" branch-rate="0">
                      <lines>
                        <line number="20" hits="0" branch="false" />
                        <line number="21" hits="0" branch="true" condition-coverage="33.3% (1/3)" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = new CoberturaCoverageParser().Parse(coveragePath);

        result.Projects.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new CoverageProjectSummary(
                ProjectName: "Sample.Project",
                CoveredLines: 2,
                TotalLines: 5,
                LineCoveragePercent: 40,
                CoveredBranches: 2,
                TotalBranches: 5,
                BranchCoveragePercent: 40));

        result.Classes.Should().HaveCount(2);
        result.Classes[0].Should().BeEquivalentTo(
            new CoverageClassCoverage(
                ProjectName: "Sample.Project",
                ClassName: "Sample.Project.Bar",
                FilePath: "Bar.cs",
                CoveredLines: 0,
                TotalLines: 2,
                LineCoveragePercent: 0,
                CoveredBranches: 1,
                TotalBranches: 3,
                BranchCoveragePercent: 33.33));
        result.Classes[1].Should().BeEquivalentTo(
            new CoverageClassCoverage(
                ProjectName: "Sample.Project",
                ClassName: "Sample.Project.Foo",
                FilePath: "Foo.cs",
                CoveredLines: 2,
                TotalLines: 3,
                LineCoveragePercent: 66.67,
                CoveredBranches: 1,
                TotalBranches: 2,
                BranchCoveragePercent: 50));
    }

    [Fact]
    public async Task Parse_ShouldAggregateEquivalentRowsAcrossMultipleDocuments()
    {
        using var fixture = new CoberturaXmlFixture();
        var firstCoveragePath = await fixture.WriteAsync(
            "first.coverage.cobertura.xml",
            """
            <coverage>
              <packages>
                <package name="Sample.Project">
                  <classes>
                    <class name="Sample.Project.Foo" filename="Foo.cs">
                      <lines>
                        <line number="10" hits="1" branch="true" condition-coverage="50% (1/2)" />
                        <line number="11" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
        var secondCoveragePath = await fixture.WriteAsync(
            "second.coverage.cobertura.xml",
            """
            <coverage>
              <packages>
                <package name="Sample.Project">
                  <classes>
                    <class name="Sample.Project.Foo" filename="Foo.cs">
                      <lines>
                        <line number="12" hits="3" branch="true" condition-coverage="100% (2/2)" />
                      </lines>
                    </class>
                    <class name="Sample.Project.Baz" filename="Baz.cs">
                      <lines>
                        <line number="20" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = new CoberturaCoverageParser().Parse(firstCoveragePath, secondCoveragePath);

        result.Classes.Should().HaveCount(2);
        result.Classes.Should().ContainSingle(row => row.ProjectName == "Sample.Project" && row.ClassName == "Sample.Project.Foo" && row.FilePath == "Foo.cs")
            .Which.Should().BeEquivalentTo(
                new CoverageClassCoverage(
                    ProjectName: "Sample.Project",
                    ClassName: "Sample.Project.Foo",
                    FilePath: "Foo.cs",
                    CoveredLines: 2,
                    TotalLines: 3,
                    LineCoveragePercent: 66.67,
                    CoveredBranches: 3,
                    TotalBranches: 4,
                    BranchCoveragePercent: 75));
        result.Projects.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new CoverageProjectSummary(
                ProjectName: "Sample.Project",
                CoveredLines: 2,
                TotalLines: 4,
                LineCoveragePercent: 50,
                CoveredBranches: 3,
                TotalBranches: 4,
                BranchCoveragePercent: 75));
    }

    [Fact]
    public async Task Parse_ShouldReturnEmptyResultWhenPackagesAreMissing()
    {
        using var fixture = new CoberturaXmlFixture();
        var coveragePath = await fixture.WriteAsync(
            "empty.coverage.cobertura.xml",
            """
            <coverage>
              <sources />
            </coverage>
            """);

        var result = new CoberturaCoverageParser().Parse(coveragePath);

        result.Projects.Should().BeEmpty();
        result.Classes.Should().BeEmpty();
    }

    [Fact]
    public async Task Parse_ShouldHandleMissingFilenameAndBranchDataWithoutThrowing()
    {
        using var fixture = new CoberturaXmlFixture();
        var coveragePath = await fixture.WriteAsync(
            "partial.coverage.cobertura.xml",
            """
            <coverage>
              <packages>
                <package name="Partial.Project">
                  <classes>
                    <class name="Partial.Project.Target">
                      <lines>
                        <line number="10" hits="1" />
                        <line number="11" hits="0" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = new CoberturaCoverageParser().Parse(coveragePath);

        result.Classes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new CoverageClassCoverage(
                ProjectName: "Partial.Project",
                ClassName: "Partial.Project.Target",
                FilePath: string.Empty,
                CoveredLines: 1,
                TotalLines: 2,
                LineCoveragePercent: 50,
                CoveredBranches: 0,
                TotalBranches: 0,
                BranchCoveragePercent: 0));
        result.Projects.Should().ContainSingle().Which.BranchCoveragePercent.Should().Be(0);
    }

    [Fact]
    public async Task Parse_ShouldReturnStableOrderingByProjectClassAndFilePath()
    {
        using var fixture = new CoberturaXmlFixture();
        var coveragePath = await fixture.WriteAsync(
            "unordered.coverage.cobertura.xml",
            """
            <coverage>
              <packages>
                <package name="Beta.Project">
                  <classes>
                    <class name="Beta.Project.Zeta" filename="Zeta.cs"><lines><line number="1" hits="1" /></lines></class>
                    <class name="Beta.Project.Alpha" filename="Alpha.cs"><lines><line number="1" hits="1" /></lines></class>
                  </classes>
                </package>
                <package name="Alpha.Project">
                  <classes>
                    <class name="Alpha.Project.Target" filename="Target.cs"><lines><line number="1" hits="1" /></lines></class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        var result = new CoberturaCoverageParser().Parse(coveragePath);

        result.Projects.Select(project => project.ProjectName).Should().Equal("Alpha.Project", "Beta.Project");
        result.Classes.Select(row => $"{row.ProjectName}|{row.ClassName}|{row.FilePath}").Should().Equal(
            "Alpha.Project|Alpha.Project.Target|Target.cs",
            "Beta.Project|Beta.Project.Alpha|Alpha.cs",
            "Beta.Project|Beta.Project.Zeta|Zeta.cs");
    }

    private sealed class CoberturaXmlFixture : IDisposable
    {
        private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "codepass-cobertura-parser-tests", Guid.NewGuid().ToString("N"));

        public CoberturaXmlFixture()
        {
            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> WriteAsync(string fileName, string xml)
        {
            var path = Path.Combine(_rootPath, fileName);
            await File.WriteAllTextAsync(path, xml);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_rootPath, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
