namespace Zeiterfassung.Core.Models;

public class WorkingTimePattern
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public decimal MondayHours { get; set; }
    public decimal TuesdayHours { get; set; }
    public decimal WednesdayHours { get; set; }
    public decimal ThursdayHours { get; set; }
    public decimal FridayHours { get; set; }
    public decimal SaturdayHours { get; set; }
    public decimal SundayHours { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
