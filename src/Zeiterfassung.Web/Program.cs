using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Core.Services;
using Zeiterfassung.Data;
using Zeiterfassung.Data.Interceptors;
using Zeiterfassung.Data.Repositories;
using Zeiterfassung.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// ── Demo mode (--demo / --mock CLI flag) ─────────────────────────────────────
var demoOptions = Zeiterfassung.Web.Services.DemoModeOptions.FromArgs(args);
builder.Services.AddSingleton(demoOptions);

// ── Stable URL when running as a published (single-file) exe ─────────────────
// Production exe binds 5000; demo binds 5001 so both can run side-by-side.
if (!builder.Environment.IsDevelopment()
    && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls(demoOptions.Enabled
        ? "http://localhost:5001"
        : "http://localhost:5000");
}

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ── Razor Pages (Login / Logout / Setup) ─────────────────────────────────────
builder.Services.AddRazorPages();

// ── Blazor Server ─────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Core Services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<HashChainService>();
builder.Services.AddScoped<PinService>();
builder.Services.AddScoped<PinAuthenticationService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<StempelService>();
builder.Services.AddScoped<StempelManager>();
builder.Services.AddScoped<SaldoService>();
builder.Services.AddScoped<ArbZGValidator>();
builder.Services.AddScoped<WorkingTimePatternService>();
builder.Services.AddScoped<LeaveEntitlementService>();

// ── Data ──────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
builder.Services.AddScoped<Zeiterfassung.Web.Services.EmployeeSessionService>();

builder.Services.AddDbContext<ZeiterfassungDbContext>((sp, options) =>
{
    // Demo mode → isolated DB file, never touches production data.
    var connectionString =
        demoOptions.Enabled
            ? "Data Source=zeiterfassung-demo.db"
            : (builder.Configuration.GetConnectionString("DefaultConnection")
               ?? "Data Source=zeiterfassung.db");
    options.UseSqlite(connectionString);
    options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
});

builder.Services.AddScoped<Zeiterfassung.Web.Services.DemoDataSeeder>();

// ── HttpContext access for Blazor ─────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Ensure DB is created (and re-seed in demo mode) ──────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ZeiterfassungDbContext>();

    if (demoOptions.Enabled)
    {
        // Demo: throw the old sandbox away and rebuild it from scratch every
        // launch so the demo is always reproducible.
        db.Database.EnsureDeleted();
    }

    db.Database.EnsureCreated();

    if (demoOptions.Enabled)
    {
        var seeder = scope.ServiceProvider.GetRequiredService<Zeiterfassung.Web.Services.DemoDataSeeder>();
        await seeder.SeedAsync(demoOptions);
    }
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ── Auto-open the user's browser when running as the published exe ──────────
// Only in Production (the single-file exe), and only the first URL the server
// bound to. Failure is silent — server still runs.
if (!app.Environment.IsDevelopment() && !Console.IsInputRedirected)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()
            ?.Addresses;
        var url = addresses?.FirstOrDefault()?.Replace("0.0.0.0", "localhost")
                                              .Replace("[::]", "localhost")
                  ?? "http://localhost:5000";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch { /* opening the browser is best-effort */ }

        Console.WriteLine();
        if (demoOptions.Enabled)
        {
            Console.WriteLine("  ╭──────────────────────────────────────────────────────╮");
            Console.WriteLine("  │  ZEITERFASSUNG ARZTPRAXIS — DEMO-MODUS               │");
            Console.WriteLine("  │  Testdaten werden bei jedem Neustart neu erzeugt.    │");
            Console.WriteLine("  ╰──────────────────────────────────────────────────────╯");
            Console.WriteLine($"  URL:        {url}");
            Console.WriteLine($"  Admin:      {demoOptions.AdminUsername} / {demoOptions.AdminPassword}");
            Console.WriteLine($"  MA-PINs:    100001 … 100006 (Reihenfolge wie Liste)");
        }
        else
        {
            Console.WriteLine($"  Zeiterfassung Arztpraxis — bereit unter {url}");
        }
        Console.WriteLine($"  Zum Beenden: STRG+C drücken oder das Fenster schließen.");
        Console.WriteLine();
    });
}

app.Run();
