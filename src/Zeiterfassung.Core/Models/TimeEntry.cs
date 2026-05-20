namespace Zeiterfassung.Core.Models;

public class TimeEntry
{
    public long Id { get; set; }
    public int EmployeeId { get; set; }
    public TimeEntryType Type { get; set; }
    public DateTime TimestampUtc { get; set; }
    public EntrySource Source { get; set; }
    public long? CorrectionOfId { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string PrevHash { get; set; } = null!;
    public string Hash { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;
    public virtual TimeEntry? CorrectionOfEntry { get; set; }
}
