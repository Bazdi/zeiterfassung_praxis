using Zeiterfassung.Web.Components;
using Zeiterfassung.Data;
using Zeiterfassung.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database
builder.Services.AddDbContext<ZeiterfassungDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Data Source=zeiterfassung.db"));

// Core Services
builder.Services.AddScoped<HashChainService>();
builder.Services.AddScoped<PinService>();
builder.Services.AddScoped<StempelService>();
builder.Services.AddScoped<SaldoService>();
builder.Services.AddScoped<ArbZGValidator>();
builder.Services.AddScoped<WorkingTimePatternService>();
builder.Services.AddScoped<LeaveEntitlementService>();

var app = builder.Build();

// Initialize database
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
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
