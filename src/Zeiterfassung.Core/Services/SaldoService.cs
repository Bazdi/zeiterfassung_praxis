using NodaTime;
using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class SaldoService
{
    private static readonly DateTimeZone BerlinTz = DateTimeZoneProviders.Tzdb["Europe/Berlin"];

    public decimal GetSaldoAsync(
        DateTime date,
        IList<TimeEntry> employeeEntries,
        IList<WorkingTimePattern> workingPatterns,
        IList<Holiday> holidays)
    {
        var localDate = LocalDate.FromDateTime(date);
        var totalWorkedHours = 0m;
        var totalRequiredHours = 0m;

        var startOfMonth = new LocalDate(date.Year, date.Month, 1);
        var endOfMonth = startOfMonth.PlusDays(DateTime.DaysInMonth(date.Year, date.Month) - 1);

        for (var d = startOfMonth; d <= endOfMonth; d = d.PlusDays(1))
        {
            var dateTime = d.AtStartOfDayInZone(BerlinTz).ToDateTimeUtc();
            var dayEntries = employeeEntries
                .Where(e => e.TimestampUtc.Date == dateTime.Date)
                .OrderBy(e => e.TimestampUtc)
                .ToList();

            var requiredHours = GetRequiredHoursForDay(d, workingPatterns, holidays);
            totalRequiredHours += requiredHours;

            if (dayEntries.Count == 0)
                continue;

            var kommenEntry = dayEntries.FirstOrDefault(e => e.Type == TimeEntryType.Kommen);
            var gehenEntry = dayEntries.LastOrDefault(e => e.Type == TimeEntryType.Gehen);

            if (kommenEntry != null && gehenEntry != null)
            {
                var workDuration = gehenEntry.TimestampUtc - kommenEntry.TimestampUtc;
                var pauseDuration = CalculatePauseDuration(dayEntries);
                var workedHours = (decimal)(workDuration.TotalMinutes - pauseDuration) / 60;
                totalWorkedHours += workedHours;
            }
        }

        return totalWorkedHours - totalRequiredHours;
    }

    private decimal GetRequiredHoursForDay(
        LocalDate date,
        IList<WorkingTimePattern> patterns,
        IList<Holiday> holidays)
    {
        if (IsHoliday(date, holidays))
            return 0;

        if (date.DayOfWeek == IsoDayOfWeek.Saturday || date.DayOfWeek == IsoDayOfWeek.Sunday)
            return 0;

        var dateTime = date.AtStartOfDayInZone(BerlinTz).ToDateTimeUtc();
        var pattern = patterns
            .Where(p => p.ValidFrom <= dateTime && (!p.ValidUntil.HasValue || p.ValidUntil >= dateTime))
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefault();

        if (pattern == null)
            return 0;

        return date.DayOfWeek switch
        {
            IsoDayOfWeek.Monday => pattern.MondayHours,
            IsoDayOfWeek.Tuesday => pattern.TuesdayHours,
            IsoDayOfWeek.Wednesday => pattern.WednesdayHours,
            IsoDayOfWeek.Thursday => pattern.ThursdayHours,
            IsoDayOfWeek.Friday => pattern.FridayHours,
            IsoDayOfWeek.Saturday => pattern.SaturdayHours,
            IsoDayOfWeek.Sunday => pattern.SundayHours,
            _ => 0
        };
    }

    private bool IsHoliday(LocalDate date, IList<Holiday> holidays)
    {
        var dateTime = date.AtStartOfDayInZone(BerlinTz).ToDateTimeUtc();
        return holidays.Any(h => h.Date.Date == dateTime.Date);
    }

    private int CalculatePauseDuration(IList<TimeEntry> dayEntries)
    {
        int totalPauseMinutes = 0;
        TimeEntry? pauseStart = null;

        foreach (var entry in dayEntries)
        {
            if (entry.Type == TimeEntryType.PauseStart)
                pauseStart = entry;
            else if (entry.Type == TimeEntryType.PauseEnd && pauseStart != null)
            {
                totalPauseMinutes += (int)(entry.TimestampUtc - pauseStart.TimestampUtc).TotalMinutes;
                pauseStart = null;
            }
        }

        return totalPauseMinutes;
    }
}
