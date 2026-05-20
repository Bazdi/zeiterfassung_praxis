using Zeiterfassung.Web.Components;
using Zeiterfassung.Data;
using Zeiterfassung.Data.Interceptors;
using Zeiterfassung.Data.Repositories;
using Zeiterfassung.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Core Services (Scoped = per request, safe for Blazor Server)
builder.Services.AddScoped<HashChainService>();
builder.Services.AddScoped<PinService>();
builder.Services.AddScoped<PinAuthenticationService>();
builder.Services.AddScoped<StempelService>();
builder.Services.AddScoped<StempelManager>();
builder.Services.AddScoped<SaldoService>();
builder.Services.AddScoped<ArbZGValidator>();
builder.Services.AddScoped<WorkingTimePatternService>();
builder.Services.AddScoped<LeaveEntitlementService>();

// Data Infrastructure
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();

// Database (must be after AuditInterceptor registration so it can be injected)
builder.Services.AddDbContext<ZeiterfassungDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=zeiterfassung.db";
    options.UseSqlite(connectionString);

    var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
    options.AddInterceptors(auditInterceptor);
});

var app = builder.Build();

// Ensure database is created on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ZeiterfassungDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
