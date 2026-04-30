using CodePass.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePass.Web.Data;

public sealed class CodePassDbContext(DbContextOptions<CodePassDbContext> options) : DbContext(options)
{
    public DbSet<RegisteredSolution> RegisteredSolutions => Set<RegisteredSolution>();
    public DbSet<AuthoredRuleDefinition> AuthoredRuleDefinitions => Set<AuthoredRuleDefinition>();
    public DbSet<SolutionRuleAssignment> SolutionRuleAssignments => Set<SolutionRuleAssignment>();
    public DbSet<RuleAnalysisRun> RuleAnalysisRuns => Set<RuleAnalysisRun>();
    public DbSet<RuleAnalysisViolation> RuleAnalysisViolations => Set<RuleAnalysisViolation>();
    public DbSet<CoverageAnalysisRun> CoverageAnalysisRuns => Set<CoverageAnalysisRun>();
    public DbSet<CoverageProjectSummary> CoverageProjectSummaries => Set<CoverageProjectSummary>();
    public DbSet<CoverageClassCoverage> CoverageClassCoverages => Set<CoverageClassCoverage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var registeredSolution = modelBuilder.Entity<RegisteredSolution>();

        registeredSolution.HasKey(solution => solution.Id);
        registeredSolution.Property(solution => solution.DisplayName).IsRequired();
        registeredSolution.Property(solution => solution.SolutionPath).IsRequired();
        registeredSolution.Property(solution => solution.Status).HasConversion<string>().IsRequired();
        registeredSolution.Property(solution => solution.QualityScorePassThreshold).IsRequired().HasDefaultValue(80);
        registeredSolution.HasIndex(solution => solution.DisplayName);
        registeredSolution.HasIndex(solution => solution.SolutionPath).IsUnique();

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

        var coverageAnalysisRun = modelBuilder.Entity<CoverageAnalysisRun>();

        coverageAnalysisRun.HasKey(run => run.Id);
        coverageAnalysisRun.Property(run => run.Status).HasConversion<string>().IsRequired();
        coverageAnalysisRun.Property(run => run.ProjectCount).IsRequired();
        coverageAnalysisRun.Property(run => run.ClassCount).IsRequired();
        coverageAnalysisRun.Property(run => run.CoveredLineCount).IsRequired();
        coverageAnalysisRun.Property(run => run.TotalLineCount).IsRequired();
        coverageAnalysisRun.Property(run => run.LineCoveragePercent).IsRequired();
        coverageAnalysisRun.Property(run => run.CoveredBranchCount).IsRequired();
        coverageAnalysisRun.Property(run => run.TotalBranchCount).IsRequired();
        coverageAnalysisRun.Property(run => run.BranchCoveragePercent).IsRequired();
        coverageAnalysisRun.Property(run => run.ErrorMessage);
        coverageAnalysisRun.HasOne<RegisteredSolution>()
            .WithMany()
            .HasForeignKey(run => run.RegisteredSolutionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        coverageAnalysisRun.HasMany(run => run.ProjectSummaries)
            .WithOne(summary => summary.CoverageAnalysisRun)
            .HasForeignKey(summary => summary.CoverageAnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        coverageAnalysisRun.HasMany(run => run.ClassCoverages)
            .WithOne(classCoverage => classCoverage.CoverageAnalysisRun)
            .HasForeignKey(classCoverage => classCoverage.CoverageAnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        coverageAnalysisRun.HasIndex(run => run.RegisteredSolutionId);
        coverageAnalysisRun.HasIndex(run => run.StartedAtUtc);
        coverageAnalysisRun.HasIndex(run => new { run.RegisteredSolutionId, run.StartedAtUtc });

        var coverageProjectSummary = modelBuilder.Entity<CoverageProjectSummary>();

        coverageProjectSummary.HasKey(summary => summary.Id);
        coverageProjectSummary.Property(summary => summary.ProjectName).IsRequired();
        coverageProjectSummary.Property(summary => summary.CoveredLineCount).IsRequired();
        coverageProjectSummary.Property(summary => summary.TotalLineCount).IsRequired();
        coverageProjectSummary.Property(summary => summary.LineCoveragePercent).IsRequired();
        coverageProjectSummary.Property(summary => summary.CoveredBranchCount).IsRequired();
        coverageProjectSummary.Property(summary => summary.TotalBranchCount).IsRequired();
        coverageProjectSummary.Property(summary => summary.BranchCoveragePercent).IsRequired();
        coverageProjectSummary.HasIndex(summary => summary.CoverageAnalysisRunId);
        coverageProjectSummary.HasIndex(summary => summary.ProjectName);

        var coverageClassCoverage = modelBuilder.Entity<CoverageClassCoverage>();

        coverageClassCoverage.HasKey(classCoverage => classCoverage.Id);
        coverageClassCoverage.Property(classCoverage => classCoverage.ProjectName).IsRequired();
        coverageClassCoverage.Property(classCoverage => classCoverage.ClassName).IsRequired();
        coverageClassCoverage.Property(classCoverage => classCoverage.FilePath).IsRequired();
        coverageClassCoverage.Property(classCoverage => classCoverage.CoveredLineCount).IsRequired();
        coverageClassCoverage.Property(classCoverage => classCoverage.TotalLineCount).IsRequired();
        coverageClassCoverage.Property(classCoverage => classCoverage.LineCoveragePercent).IsRequired();
        coverageClassCoverage.Property(classCoverage => classCoverage.CoveredBranchCount).IsRequired();
        coverageClassCoverage.Property(classCoverage => classCoverage.TotalBranchCount).IsRequired();
        coverageClassCoverage.Property(classCoverage => classCoverage.BranchCoveragePercent).IsRequired();
        coverageClassCoverage.HasIndex(classCoverage => classCoverage.CoverageAnalysisRunId);
        coverageClassCoverage.HasIndex(classCoverage => classCoverage.ClassName);
    }
}
