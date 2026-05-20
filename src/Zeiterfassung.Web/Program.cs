using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Core.Services;
using Zeiterfassung.Data;
using Zeiterfassung.Data.Interceptors;
using Zeiterfassung.Data.Repositories;
using Zeiterfassung.Web.Components;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddDbContext<ZeiterfassungDbContext>((sp, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=zeiterfassung.db";
    options.UseSqlite(connectionString);
    options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
});

// ── HttpContext access for Blazor ─────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Ensure DB is created ──────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ZeiterfassungDbContext>();
    db.Database.EnsureCreated();
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

app.Run();
