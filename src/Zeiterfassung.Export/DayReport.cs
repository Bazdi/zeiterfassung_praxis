namespace Zeiterfassung.Export;

public class DayReport
{
    public DateTime Date { get; set; }
    public DateTime? KommenLocal { get; set; }
    public DateTime? GehenLocal { get; set; }
    public int PauseMinutes { get; set; }
    public decimal WorkedHours { get; set; }
    public decimal RequiredHours { get; set; }
    public decimal BalanceHours { get; set; }
    public bool IsHoliday { get; set; }
    public string? HolidayName { get; set; }
    public bool HasWarning { get; set; }
    public string? WarningText { get; set; }
}

public class MonthReport
{
    public string EmployeeName { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public List<DayReport> Days { get; set; } = new();
    public decimal TotalWorkedHours => Days.Sum(d => d.WorkedHours);
    public decimal TotalRequiredHours => Days.Sum(d => d.RequiredHours);
    public decimal TotalBalance => TotalWorkedHours - TotalRequiredHours;
    public string PracticeName { get; set; } = "Arztpraxis";
}
