using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zeiterfassung.Core.Services;
using Zeiterfassung.Data;

namespace Zeiterfassung.Web.Pages;

public class LoginModel : PageModel
{
    private readonly ZeiterfassungDbContext _db;
    private readonly AdminAuthService _auth;

    public string? ErrorMessage { get; private set; }
    public string? Username { get; private set; }

    public LoginModel(ZeiterfassungDbContext db, AdminAuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        // If already logged in, redirect
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(returnUrl ?? "/");

        // If no admin exists yet, go to setup
        var hasAdmin = await _db.Users.AnyAsync(u => u.Roles.Contains("Admin"));
        if (!hasAdmin)
            return Redirect("/setup");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string? Username,
        string? Password,
        string? returnUrl)
    {
        this.Username = Username;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Bitte Benutzername und Passwort eingeben.";
            return Page();
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == Username.Trim().ToLowerInvariant()
                                   && u.Roles.Contains("Admin"));

        if (user == null || !_auth.VerifyPassword(Password, user.PasswordHash))
        {
            ErrorMessage = "Ungültiger Benutzername oder Passwort.";
            return Page();
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Sign in with cookie
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });

        var target = (returnUrl != null && Url.IsLocalUrl(returnUrl)) ? returnUrl : "/";
        return Redirect(target);
    }
}
