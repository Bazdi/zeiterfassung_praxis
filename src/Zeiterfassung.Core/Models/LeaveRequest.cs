using System.ComponentModel.DataAnnotations;

namespace Zeiterfassung.Core.Models;

public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public LeaveType Type { get; set; }
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Open;
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // DSGVO Art. 9: Krankmeldungen sind Gesundheitsdaten.
    // Notiz darf NUR bei Typ != Krank gesetzt sein.
    // Wird serverseitig in CreateLeaveRequest validiert und via DB-Constraint (SQLite CHECK) gesichert.
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public bool IsNotesAllowed => Type != LeaveType.Krank;
}
