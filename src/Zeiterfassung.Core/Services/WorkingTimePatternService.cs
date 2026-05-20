using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class WorkingTimePatternService
{
    /// <summary>
    /// Returns the working time pattern valid for a given date.
    /// Uses date-only comparison to avoid time-component issues.
    /// </summary>
    public WorkingTimePattern? GetPatternForDate(
        DateTime date,
        IList<WorkingTimePattern> patterns)
    {
        var dateOnly = date.Date;
        return patterns
            .Where(p => p.ValidFrom.Date <= dateOnly &&
                        (!p.ValidUntil.HasValue || p.ValidUntil.Value.Date >= dateOnly))
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefault();
    }

    public decimal GetHoursForDay(
        DayOfWeek dayOfWeek,
        WorkingTimePattern pattern)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => pattern.MondayHours,
            DayOfWeek.Tuesday => pattern.TuesdayHours,
            DayOfWeek.Wednesday => pattern.WednesdayHours,
            DayOfWeek.Thursday => pattern.ThursdayHours,
            DayOfWeek.Friday => pattern.FridayHours,
            DayOfWeek.Saturday => pattern.SaturdayHours,
            DayOfWeek.Sunday => pattern.SundayHours,
            _ => 0
        };
    }

    /// <summary>
    /// Closes an existing pattern by setting its ValidUntil to yesterday.
    /// Call before creating a new pattern via DB.
    /// </summary>
    public void EndPattern(WorkingTimePattern pattern, DateTime newPatternStart)
    {
        pattern.ValidUntil = newPatternStart.Date.AddDays(-1);
    }
}
