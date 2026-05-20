namespace Zeiterfassung.Core.Models;

public class LeaveEntitlement
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int Year { get; set; }
    public decimal EntitlementDays { get; set; }
    public decimal CarriedOverDays { get; set; }
    public decimal SpecialLeaveDays { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
