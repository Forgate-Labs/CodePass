using CodePass.Web.Components;
using CodePass.Web.Data;
using CodePass.Web.Services.Solutions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<CodePassDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("CodePass")));
builder.Services.AddScoped<ISolutionPathValidator, SolutionPathValidator>();
builder.Services.AddScoped<IRegisteredSolutionService, RegisteredSolutionService>();
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
    dbContext.Database.EnsureCreated();
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
