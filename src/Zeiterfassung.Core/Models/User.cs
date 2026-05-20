namespace Zeiterfassung.Core.Models;

public class User
{
    public int Id { get; set; }
    public int? EmployeeId { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Roles { get; set; } = "User";
    public string? TotpSecret { get; set; }
    public bool IsTotpEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public virtual Employee? Employee { get; set; }
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
