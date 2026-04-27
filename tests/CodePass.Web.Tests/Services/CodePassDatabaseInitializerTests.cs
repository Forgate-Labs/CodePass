using CodePass.Web.Data;
using CodePass.Web.Data.Entities;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Tests.Services;

public sealed class CodePassDatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_ShouldAddAuthoredRulesTableToExistingLegacySqliteDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"codepass-init-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            await CreateLegacyDatabaseAsync(connectionString);

            var options = new DbContextOptionsBuilder<CodePassDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using var dbContext = new CodePassDbContext(options);
            await CodePassDatabaseInitializer.InitializeAsync(dbContext);

            var tableNames = await ReadTableNamesAsync(connectionString);
            tableNames.Should().Contain("RegisteredSolutions");
            tableNames.Should().Contain("AuthoredRuleDefinitions");
            tableNames.Should().Contain("SolutionRuleAssignments");

            var existingSolution = await dbContext.RegisteredSolutions.SingleAsync();
            existingSolution.DisplayName.Should().Be("Legacy solution");

            dbContext.AuthoredRuleDefinitions.Add(new AuthoredRuleDefinition
            {
                Id = Guid.NewGuid(),
                Code = "CP2000",
                Title = "Added after upgrade",
                Description = null,
                RuleKind = "syntax_presence",
                SchemaVersion = "1.0",
                Severity = RuleSeverity.Warning,
                ScopeJson = "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}",
                ParametersJson = "{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"],\"allowInTests\":false}",
                RawDefinitionJson = "{\"id\":\"CP2000\",\"title\":\"Added after upgrade\",\"kind\":\"syntax_presence\",\"schemaVersion\":\"1.0\",\"severity\":\"warning\",\"enabled\":true,\"language\":\"csharp\",\"scope\":{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]},\"parameters\":{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"],\"allowInTests\":false}}",
                IsEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync();

            (await dbContext.AuthoredRuleDefinitions.CountAsync()).Should().Be(1);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateCoverageTablesAndIndexesForFreshSqliteDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"codepass-init-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            var options = new DbContextOptionsBuilder<CodePassDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using var dbContext = new CodePassDbContext(options);
            await CodePassDatabaseInitializer.InitializeAsync(dbContext);

            var tableNames = await ReadTableNamesAsync(connectionString);
            tableNames.Should().Contain("CoverageAnalysisRuns");
            tableNames.Should().Contain("CoverageProjectSummaries");
            tableNames.Should().Contain("CoverageClassCoverages");

            var indexNames = await ReadIndexNamesAsync(connectionString);
            indexNames.Should().Contain("IX_CoverageAnalysisRuns_RegisteredSolutionId");
            indexNames.Should().Contain("IX_CoverageAnalysisRuns_StartedAtUtc");
            indexNames.Should().Contain("IX_CoverageAnalysisRuns_RegisteredSolutionId_StartedAtUtc");
            indexNames.Should().Contain("IX_CoverageProjectSummaries_CoverageAnalysisRunId");
            indexNames.Should().Contain("IX_CoverageProjectSummaries_ProjectName");
            indexNames.Should().Contain("IX_CoverageClassCoverages_CoverageAnalysisRunId");
            indexNames.Should().Contain("IX_CoverageClassCoverages_ClassName");

            var solutionId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            dbContext.RegisteredSolutions.Add(new RegisteredSolution
            {
                Id = solutionId,
                DisplayName = "Fresh coverage solution",
                SolutionPath = "/tmp/fresh-coverage.sln",
                Status = RegisteredSolutionStatus.Valid,
                LastValidatedAtUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            dbContext.CoverageAnalysisRuns.Add(new CoverageAnalysisRun
            {
                Id = runId,
                RegisteredSolutionId = solutionId,
                Status = CoverageAnalysisRunStatus.Succeeded,
                StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                CompletedAtUtc = DateTimeOffset.UtcNow,
                ProjectCount = 1,
                ClassCount = 1,
                CoveredLineCount = 9,
                TotalLineCount = 10,
                LineCoveragePercent = 90,
                CoveredBranchCount = 3,
                TotalBranchCount = 4,
                BranchCoveragePercent = 75
            });
            dbContext.CoverageProjectSummaries.Add(new CoverageProjectSummary
            {
                Id = Guid.NewGuid(),
                CoverageAnalysisRunId = runId,
                ProjectName = "CodePass.Web",
                CoveredLineCount = 9,
                TotalLineCount = 10,
                LineCoveragePercent = 90,
                CoveredBranchCount = 3,
                TotalBranchCount = 4,
                BranchCoveragePercent = 75
            });
            dbContext.CoverageClassCoverages.Add(new CoverageClassCoverage
            {
                Id = Guid.NewGuid(),
                CoverageAnalysisRunId = runId,
                ProjectName = "CodePass.Web",
                ClassName = "CoverageAnalyzer",
                FilePath = "src/CodePass.Web/Services/CoverageAnalysis/CoverageAnalyzer.cs",
                CoveredLineCount = 9,
                TotalLineCount = 10,
                LineCoveragePercent = 90,
                CoveredBranchCount = 3,
                TotalBranchCount = 4,
                BranchCoveragePercent = 75
            });

            await dbContext.SaveChangesAsync();

            (await dbContext.CoverageAnalysisRuns.CountAsync()).Should().Be(1);
            (await dbContext.CoverageProjectSummaries.CountAsync()).Should().Be(1);
            (await dbContext.CoverageClassCoverages.CountAsync()).Should().Be(1);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldAddCoverageTablesWithoutLosingExistingLegacyRuleAnalysisData()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"codepass-init-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            var existingSolutionId = Guid.NewGuid();
            var existingRuleId = Guid.NewGuid();
            var existingAssignmentId = Guid.NewGuid();
            var existingAnalysisRunId = Guid.NewGuid();
            await CreateLegacyDatabaseWithRuleAnalysisAsync(
                connectionString,
                existingSolutionId,
                existingRuleId,
                existingAssignmentId,
                existingAnalysisRunId);

            var options = new DbContextOptionsBuilder<CodePassDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using var dbContext = new CodePassDbContext(options);
            await CodePassDatabaseInitializer.InitializeAsync(dbContext);

            var tableNames = await ReadTableNamesAsync(connectionString);
            tableNames.Should().Contain("RegisteredSolutions");
            tableNames.Should().Contain("AuthoredRuleDefinitions");
            tableNames.Should().Contain("SolutionRuleAssignments");
            tableNames.Should().Contain("RuleAnalysisRuns");
            tableNames.Should().Contain("RuleAnalysisViolations");
            tableNames.Should().Contain("CoverageAnalysisRuns");
            tableNames.Should().Contain("CoverageProjectSummaries");
            tableNames.Should().Contain("CoverageClassCoverages");

            var indexNames = await ReadIndexNamesAsync(connectionString);
            indexNames.Should().Contain("IX_CoverageAnalysisRuns_RegisteredSolutionId_StartedAtUtc");
            indexNames.Should().Contain("IX_CoverageProjectSummaries_CoverageAnalysisRunId");
            indexNames.Should().Contain("IX_CoverageClassCoverages_CoverageAnalysisRunId");
            indexNames.Should().Contain("IX_CoverageProjectSummaries_ProjectName");
            indexNames.Should().Contain("IX_CoverageClassCoverages_ClassName");

            (await dbContext.RegisteredSolutions.CountAsync()).Should().Be(1);
            (await dbContext.AuthoredRuleDefinitions.CountAsync()).Should().Be(1);
            (await dbContext.SolutionRuleAssignments.CountAsync()).Should().Be(1);
            (await dbContext.RuleAnalysisRuns.CountAsync()).Should().Be(1);
            (await dbContext.RuleAnalysisViolations.CountAsync()).Should().Be(1);

            var coverageRunId = Guid.NewGuid();
            dbContext.CoverageAnalysisRuns.Add(new CoverageAnalysisRun
            {
                Id = coverageRunId,
                RegisteredSolutionId = existingSolutionId,
                Status = CoverageAnalysisRunStatus.Succeeded,
                StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                CompletedAtUtc = DateTimeOffset.UtcNow,
                ProjectCount = 1,
                ClassCount = 1,
                CoveredLineCount = 12,
                TotalLineCount = 16,
                LineCoveragePercent = 75,
                CoveredBranchCount = 2,
                TotalBranchCount = 8,
                BranchCoveragePercent = 25
            });
            dbContext.CoverageProjectSummaries.Add(new CoverageProjectSummary
            {
                Id = Guid.NewGuid(),
                CoverageAnalysisRunId = coverageRunId,
                ProjectName = "Legacy.Web",
                CoveredLineCount = 12,
                TotalLineCount = 16,
                LineCoveragePercent = 75,
                CoveredBranchCount = 2,
                TotalBranchCount = 8,
                BranchCoveragePercent = 25
            });
            dbContext.CoverageClassCoverages.Add(new CoverageClassCoverage
            {
                Id = Guid.NewGuid(),
                CoverageAnalysisRunId = coverageRunId,
                ProjectName = "Legacy.Web",
                ClassName = "LegacyController",
                FilePath = "src/Legacy.Web/LegacyController.cs",
                CoveredLineCount = 12,
                TotalLineCount = 16,
                LineCoveragePercent = 75,
                CoveredBranchCount = 2,
                TotalBranchCount = 8,
                BranchCoveragePercent = 25
            });

            await dbContext.SaveChangesAsync();

            (await dbContext.CoverageAnalysisRuns.CountAsync()).Should().Be(1);
            (await dbContext.CoverageProjectSummaries.CountAsync()).Should().Be(1);
            (await dbContext.CoverageClassCoverages.CountAsync()).Should().Be(1);
            var existingSolution = await dbContext.RegisteredSolutions.SingleAsync();
            existingSolution.DisplayName.Should().Be("Legacy solution with rule analysis");
            var existingRule = await dbContext.AuthoredRuleDefinitions.SingleAsync();
            existingRule.Code.Should().Be("CP3000");
            var existingViolation = await dbContext.RuleAnalysisViolations.SingleAsync();
            existingViolation.FilePath.Should().Be("src/Legacy.cs");
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static async Task CreateLegacyDatabaseAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LegacyCodePassDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var legacyContext = new LegacyCodePassDbContext(options);
        await legacyContext.Database.EnsureCreatedAsync();
        legacyContext.RegisteredSolutions.Add(new RegisteredSolution
        {
            Id = Guid.NewGuid(),
            DisplayName = "Legacy solution",
            SolutionPath = "/tmp/legacy.sln",
            Status = RegisteredSolutionStatus.Valid,
            LastValidatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await legacyContext.SaveChangesAsync();
    }

    private static async Task CreateLegacyDatabaseWithRuleAnalysisAsync(
        string connectionString,
        Guid solutionId,
        Guid ruleId,
        Guid assignmentId,
        Guid analysisRunId)
    {
        var options = new DbContextOptionsBuilder<LegacyCodePassDbContextWithRuleAnalysis>()
            .UseSqlite(connectionString)
            .Options;

        await using var legacyContext = new LegacyCodePassDbContextWithRuleAnalysis(options);
        await legacyContext.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        legacyContext.RegisteredSolutions.Add(new RegisteredSolution
        {
            Id = solutionId,
            DisplayName = "Legacy solution with rule analysis",
            SolutionPath = "/tmp/legacy-with-rule-analysis.sln",
            Status = RegisteredSolutionStatus.Valid,
            LastValidatedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        legacyContext.AuthoredRuleDefinitions.Add(new AuthoredRuleDefinition
        {
            Id = ruleId,
            Code = "CP3000",
            Title = "Existing authored rule",
            Description = null,
            RuleKind = "syntax_presence",
            SchemaVersion = "1.0",
            Severity = RuleSeverity.Error,
            ScopeJson = "{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]}",
            ParametersJson = "{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"],\"allowInTests\":false}",
            RawDefinitionJson = "{\"id\":\"CP3000\",\"title\":\"Existing authored rule\",\"kind\":\"syntax_presence\",\"schemaVersion\":\"1.0\",\"severity\":\"error\",\"enabled\":true,\"language\":\"csharp\",\"scope\":{\"projects\":[\"*\"],\"files\":[\"**/*.cs\"],\"excludeFiles\":[]},\"parameters\":{\"mode\":\"forbid\",\"targets\":[\"local_declaration\"],\"syntaxKinds\":[\"var\"],\"allowInTests\":false}}",
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        legacyContext.SolutionRuleAssignments.Add(new SolutionRuleAssignment
        {
            Id = assignmentId,
            RegisteredSolutionId = solutionId,
            AuthoredRuleDefinitionId = ruleId,
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        legacyContext.RuleAnalysisRuns.Add(new RuleAnalysisRun
        {
            Id = analysisRunId,
            RegisteredSolutionId = solutionId,
            Status = RuleAnalysisRunStatus.Succeeded,
            StartedAtUtc = now.AddMinutes(-1),
            CompletedAtUtc = now,
            RuleCount = 1,
            TotalViolations = 1
        });
        legacyContext.RuleAnalysisViolations.Add(new RuleAnalysisViolation
        {
            Id = Guid.NewGuid(),
            RuleAnalysisRunId = analysisRunId,
            AuthoredRuleDefinitionId = ruleId,
            RuleCode = "CP3000",
            RuleTitle = "Existing authored rule",
            RuleKind = "syntax_presence",
            RuleSeverity = RuleSeverity.Error,
            Message = "Use explicit type instead of var.",
            FilePath = "src/Legacy.cs",
            StartLine = 10,
            StartColumn = 5,
            EndLine = 10,
            EndColumn = 8
        });
        await legacyContext.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadTableNamesAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync();

        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static async Task<IReadOnlyList<string>> ReadIndexNamesAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync();

        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private sealed class LegacyCodePassDbContext(DbContextOptions<LegacyCodePassDbContext> options) : DbContext(options)
    {
        public DbSet<RegisteredSolution> RegisteredSolutions => Set<RegisteredSolution>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureRegisteredSolution(modelBuilder);
        }
    }

    private sealed class LegacyCodePassDbContextWithRuleAnalysis(DbContextOptions<LegacyCodePassDbContextWithRuleAnalysis> options) : DbContext(options)
    {
        public DbSet<RegisteredSolution> RegisteredSolutions => Set<RegisteredSolution>();

        public DbSet<AuthoredRuleDefinition> AuthoredRuleDefinitions => Set<AuthoredRuleDefinition>();

        public DbSet<SolutionRuleAssignment> SolutionRuleAssignments => Set<SolutionRuleAssignment>();

        public DbSet<RuleAnalysisRun> RuleAnalysisRuns => Set<RuleAnalysisRun>();

        public DbSet<RuleAnalysisViolation> RuleAnalysisViolations => Set<RuleAnalysisViolation>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureRegisteredSolution(modelBuilder);
            ConfigureAuthoredRuleDefinition(modelBuilder);
            ConfigureSolutionRuleAssignment(modelBuilder);
            ConfigureRuleAnalysisRun(modelBuilder);
            ConfigureRuleAnalysisViolation(modelBuilder);
        }
    }

    private static void ConfigureRegisteredSolution(ModelBuilder modelBuilder)
    {
        var registeredSolution = modelBuilder.Entity<RegisteredSolution>();

        registeredSolution.HasKey(solution => solution.Id);
        registeredSolution.Property(solution => solution.DisplayName).IsRequired();
        registeredSolution.Property(solution => solution.SolutionPath).IsRequired();
        registeredSolution.Property(solution => solution.Status).HasConversion<string>().IsRequired();
        registeredSolution.HasIndex(solution => solution.DisplayName);
        registeredSolution.HasIndex(solution => solution.SolutionPath).IsUnique();
    }

    private static void ConfigureAuthoredRuleDefinition(ModelBuilder modelBuilder)
    {
        var authoredRuleDefinition = modelBuilder.Entity<AuthoredRuleDefinition>();

        authoredRuleDefinition.HasKey(rule => rule.Id);
        authoredRuleDefinition.Property(rule => rule.Code).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Title).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Description);
        authoredRuleDefinition.Property(rule => rule.RuleKind).IsRequired();
        authoredRuleDefinition.Property(rule => rule.SchemaVersion).IsRequired();
        authoredRuleDefinition.Property(rule => rule.Severity).HasConversion<string>().IsRequired();
        authoredRuleDefinition.Property(rule => rule.ScopeJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.ParametersJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.RawDefinitionJson).IsRequired();
        authoredRuleDefinition.Property(rule => rule.IsEnabled).IsRequired();
        authoredRuleDefinition.HasIndex(rule => rule.Code).IsUnique();
        authoredRuleDefinition.HasIndex(rule => rule.RuleKind);
    }

    private static void ConfigureSolutionRuleAssignment(ModelBuilder modelBuilder)
    {
        var solutionRuleAssignment = modelBuilder.Entity<SolutionRuleAssignment>();

        solutionRuleAssignment.HasKey(assignment => assignment.Id);
        solutionRuleAssignment.Property(assignment => assignment.IsEnabled).IsRequired();
        solutionRuleAssignment.HasOne<RegisteredSolution>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RegisteredSolutionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        solutionRuleAssignment.HasOne<AuthoredRuleDefinition>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AuthoredRuleDefinitionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        solutionRuleAssignment.HasIndex(assignment => new { assignment.RegisteredSolutionId, assignment.AuthoredRuleDefinitionId })
            .IsUnique();
    }

    private static void ConfigureRuleAnalysisRun(ModelBuilder modelBuilder)
    {
        var ruleAnalysisRun = modelBuilder.Entity<RuleAnalysisRun>();

        ruleAnalysisRun.HasKey(run => run.Id);
        ruleAnalysisRun.Property(run => run.Status).HasConversion<string>().IsRequired();
        ruleAnalysisRun.Property(run => run.RuleCount).IsRequired();
        ruleAnalysisRun.Property(run => run.TotalViolations).IsRequired();
        ruleAnalysisRun.Property(run => run.ErrorMessage);
        ruleAnalysisRun.HasOne<RegisteredSolution>()
            .WithMany()
            .HasForeignKey(run => run.RegisteredSolutionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        ruleAnalysisRun.HasMany(run => run.Violations)
            .WithOne(violation => violation.RuleAnalysisRun)
            .HasForeignKey(violation => violation.RuleAnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        ruleAnalysisRun.HasIndex(run => run.RegisteredSolutionId);
        ruleAnalysisRun.HasIndex(run => run.StartedAtUtc);
    }

    private static void ConfigureRuleAnalysisViolation(ModelBuilder modelBuilder)
    {
        var ruleAnalysisViolation = modelBuilder.Entity<RuleAnalysisViolation>();

        ruleAnalysisViolation.HasKey(violation => violation.Id);
        ruleAnalysisViolation.Property(violation => violation.RuleCode).IsRequired();
        ruleAnalysisViolation.Property(violation => violation.RuleTitle).IsRequired();
        ruleAnalysisViolation.Property(violation => violation.RuleKind).IsRequired();
        ruleAnalysisViolation.Property(violation => violation.RuleSeverity).HasConversion<string>().IsRequired();
        ruleAnalysisViolation.Property(violation => violation.Message).IsRequired();
        ruleAnalysisViolation.Property(violation => violation.FilePath).IsRequired();
        ruleAnalysisViolation.HasOne<AuthoredRuleDefinition>()
            .WithMany()
            .HasForeignKey(violation => violation.AuthoredRuleDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);
        ruleAnalysisViolation.HasIndex(violation => violation.RuleAnalysisRunId);
        ruleAnalysisViolation.HasIndex(violation => violation.RuleCode);
    }
}
