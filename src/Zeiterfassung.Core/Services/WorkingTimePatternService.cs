using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class WorkingTimePatternService
{
    public WorkingTimePattern? GetPatternForDate(
        DateTime date,
        IList<WorkingTimePattern> patterns)
    {
        return patterns
            .Where(p => p.ValidFrom <= date && (!p.ValidUntil.HasValue || p.ValidUntil >= date))
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

    public void EndPreviousPattern(WorkingTimePattern pattern)
    {
        if (pattern.ValidUntil == null)
        {
            pattern.ValidUntil = DateTime.Today.AddDays(-1);
        }
    }
}
