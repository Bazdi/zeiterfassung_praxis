using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Zeiterfassung.Core.Services;
using Zeiterfassung.Data;

namespace Zeiterfassung.Web.Pages;

public class SetupModel : PageModel
{
    private readonly ZeiterfassungDbContext _db;
    private readonly AdminAuthService _auth;

    public string? ErrorMessage { get; private set; }
    public string? Username { get; private set; }
    public string? DisplayName { get; private set; }

    public SetupModel(ZeiterfassungDbContext db, AdminAuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Lock setup if admin already exists
        if (await _db.Users.AnyAsync(u => u.Roles.Contains("Admin")))
            return Redirect("/login");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string? Username,
        string? DisplayName,
        string? Password,
        string? Confirm)
    {
        this.Username = Username;
        this.DisplayName = DisplayName;

        // Guard: still no admin?
        if (await _db.Users.AnyAsync(u => u.Roles.Contains("Admin")))
            return Redirect("/login");

        if (string.IsNullOrWhiteSpace(Username) || Username.Contains(' '))
        {
            ErrorMessage = "Benutzername darf keine Leerzeichen enthalten.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Password) || !_auth.IsValidPassword(Password))
        {
            ErrorMessage = "Passwort muss mind. 10 Zeichen, 1 Großbuchstabe und 1 Zahl enthalten.";
            return Page();
        }

        if (Password != Confirm)
        {
            ErrorMessage = "Passwörter stimmen nicht überein.";
            return Page();
        }

        var user = _auth.CreateAdminUser(Username, Password);

        // Optionally link to an Employee record for the display name
        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            var parts = DisplayName.Trim().Split(' ', 2);
            var employee = new Core.Models.Employee
            {
                FirstName = parts.Length > 1 ? parts[0] : DisplayName.Trim(),
                LastName = parts.Length > 1 ? parts[1] : "",
                IsAdmin = true,
                IsActive = true,
                PinHash = "setup",
                PinSalt = "setup",
                CreatedAt = DateTime.UtcNow
            };
            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();
            user.EmployeeId = employee.Id;
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Auto-login after setup
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) });

        return Redirect("/");
    }
}
