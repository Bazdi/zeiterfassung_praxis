namespace Zeiterfassung.Core.Models;

public class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? Email { get; set; }
    public string PinHash { get; set; } = null!;
    public string PinSalt { get; set; } = null!;
    public DateTime? PinChangedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; } = false;
    public int FailedPinAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public virtual ICollection<WorkingTimePattern> WorkingTimePatterns { get; set; } = new List<WorkingTimePattern>();
    public virtual ICollection<LeaveEntitlement> LeaveEntitlements { get; set; } = new List<LeaveEntitlement>();
    public virtual ICollection<CorrectionRequest> CorrectionRequests { get; set; } = new List<CorrectionRequest>();
    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
