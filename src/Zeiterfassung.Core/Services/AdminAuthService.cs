using BCrypt.Net;
using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class AdminAuthService
{
    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);

    public bool IsValidPassword(string password)
    {
        if (password.Length < 10) return false;
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        return hasUpper && hasDigit;
    }

    public User CreateAdminUser(string username, string password, int? employeeId = null)
    {
        return new User
        {
            Username = username.Trim().ToLowerInvariant(),
            PasswordHash = HashPassword(password),
            Roles = "Admin",
            EmployeeId = employeeId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
