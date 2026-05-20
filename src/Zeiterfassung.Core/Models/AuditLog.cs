namespace Zeiterfassung.Core.Models;

public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string? OldJson { get; set; }
    public string? NewJson { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string PrevHash { get; set; } = null!;
    public string Hash { get; set; } = null!;

    public virtual User? User { get; set; }
}
