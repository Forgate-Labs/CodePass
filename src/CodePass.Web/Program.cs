using CodePass.Web.Components;
using CodePass.Web.Data;
using CodePass.Web.Services.CoverageAnalysis;
using CodePass.Web.Services.Dashboard;
using CodePass.Web.Services.RuleAnalysis;
using CodePass.Web.Services.Rules;
using CodePass.Web.Services.Solutions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<CodePassDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("CodePass")));
builder.Services.AddScoped<ISolutionPathValidator, SolutionPathValidator>();
builder.Services.AddScoped<IRegisteredSolutionService, RegisteredSolutionService>();
builder.Services.AddScoped<IRuleCatalogService, RuleCatalogService>();
builder.Services.AddScoped<IRuleDefinitionService, RuleDefinitionService>();
builder.Services.AddScoped<ISolutionRuleSelectionService, SolutionRuleSelectionService>();
builder.Services.AddScoped<IRuleAnalyzer, RoslynRuleAnalyzer>();
builder.Services.AddScoped<IRuleAnalysisResultService, RuleAnalysisResultService>();
builder.Services.AddScoped<IRuleAnalysisRunService, RuleAnalysisRunService>();
builder.Services.AddScoped<ICoverageAnalyzer, DotNetCoverageAnalyzer>();
builder.Services.AddScoped<ICoverageAnalysisResultService, CoverageAnalysisResultService>();
builder.Services.AddScoped<ICoverageAnalysisRunService, CoverageAnalysisRunService>();
builder.Services.AddScoped<IQualityScoreService, QualityScoreService>();
builder.Services.AddHostedService<SolutionStatusRefreshService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CodePassDbContext>();
    await CodePassDatabaseInitializer.InitializeAsync(dbContext);
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
