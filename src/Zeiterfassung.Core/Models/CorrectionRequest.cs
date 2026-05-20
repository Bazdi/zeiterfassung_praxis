namespace Zeiterfassung.Core.Models;

public class CorrectionRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime RequestedTimestamp { get; set; }
    public TimeEntryType Type { get; set; }
    public string Reason { get; set; } = null!;
    public CorrectionStatus Status { get; set; } = CorrectionStatus.Open;
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public long? ResultingEntryId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;
    public virtual TimeEntry? ResultingEntry { get; set; }
}
