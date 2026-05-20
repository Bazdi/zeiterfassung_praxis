using NodaTime;
using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class SaldoService
{
    private static readonly DateTimeZone BerlinTz = DateTimeZoneProviders.Tzdb["Europe/Berlin"];

    /// <summary>
    /// Calculates worked hours minus required hours for the given month.
    /// Uses NodaTime for DST-safe day boundaries (handles 23h/25h days in March/October).
    /// </summary>
    public decimal CalculateMonthlyBalance(
        int year,
        int month,
        IList<TimeEntry> employeeEntries,
        IList<WorkingTimePattern> workingPatterns,
        IList<Holiday> holidays)
    {
        var totalWorkedMinutes = 0m;
        var totalRequiredMinutes = 0m;

        var startOfMonth = new LocalDate(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        for (var dayOffset = 0; dayOffset < daysInMonth; dayOffset++)
        {
            var localDate = startOfMonth.PlusDays(dayOffset);

            // DST-safe: get UTC interval for this local calendar day in Berlin timezone
            var startOfDayBerlin = localDate.AtStartOfDayInZone(BerlinTz);
            var endOfDayBerlin = localDate.PlusDays(1).AtStartOfDayInZone(BerlinTz);

            var startUtc = startOfDayBerlin.ToDateTimeUtc();
            var endUtc = endOfDayBerlin.ToDateTimeUtc();

            var dayEntries = employeeEntries
                .Where(e => e.TimestampUtc >= startUtc && e.TimestampUtc < endUtc)
                .OrderBy(e => e.TimestampUtc)
                .ToList();

            var requiredMinutes = GetRequiredMinutesForDay(localDate, workingPatterns, holidays);
            totalRequiredMinutes += requiredMinutes;

            if (dayEntries.Count == 0)
                continue;

            var workedMinutes = CalculateWorkedMinutes(dayEntries);
            totalWorkedMinutes += workedMinutes;
        }

        return (totalWorkedMinutes - totalRequiredMinutes) / 60m;
    }

    public decimal CalculateDailyBalance(
        LocalDate date,
        IList<TimeEntry> dayEntries,
        IList<WorkingTimePattern> workingPatterns,
        IList<Holiday> holidays)
    {
        var required = GetRequiredMinutesForDay(date, workingPatterns, holidays);
        var worked = CalculateWorkedMinutes(dayEntries.OrderBy(e => e.TimestampUtc).ToList());
        return (worked - required) / 60m;
    }

    public decimal CalculateWorkedHours(IList<TimeEntry> dayEntries)
    {
        return CalculateWorkedMinutes(dayEntries.OrderBy(e => e.TimestampUtc).ToList()) / 60m;
    }

    private decimal CalculateWorkedMinutes(IList<TimeEntry> sortedDayEntries)
    {
        var kommenEntry = sortedDayEntries.FirstOrDefault(e => e.Type == TimeEntryType.Kommen);
        var gehenEntry = sortedDayEntries.LastOrDefault(e => e.Type == TimeEntryType.Gehen);

        if (kommenEntry == null || gehenEntry == null)
            return 0;

        var totalMinutes = (decimal)(gehenEntry.TimestampUtc - kommenEntry.TimestampUtc).TotalMinutes;
        var pauseMinutes = CalculatePauseMinutes(sortedDayEntries);

        return Math.Max(0, totalMinutes - pauseMinutes);
    }

    private decimal GetRequiredMinutesForDay(
        LocalDate date,
        IList<WorkingTimePattern> patterns,
        IList<Holiday> holidays)
    {
        if (IsHoliday(date, holidays))
            return 0;

        if (date.DayOfWeek == IsoDayOfWeek.Saturday || date.DayOfWeek == IsoDayOfWeek.Sunday)
            return 0;

        var dateUtc = date.AtStartOfDayInZone(BerlinTz).ToDateTimeUtc();
        var pattern = patterns
            .Where(p => p.ValidFrom.Date <= dateUtc.Date &&
                        (!p.ValidUntil.HasValue || p.ValidUntil.Value.Date >= dateUtc.Date))
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefault();

        if (pattern == null)
            return 0;

        var dailyHours = date.DayOfWeek switch
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

        return dailyHours * 60m;
    }

    private bool IsHoliday(LocalDate date, IList<Holiday> holidays)
    {
        var dateUtc = date.AtStartOfDayInZone(BerlinTz).ToDateTimeUtc();
        return holidays.Any(h => h.Date.Date == dateUtc.Date);
    }

    private static decimal CalculatePauseMinutes(IList<TimeEntry> dayEntries)
    {
        decimal totalPauseMinutes = 0;
        TimeEntry? pauseStart = null;

        foreach (var entry in dayEntries)
        {
            if (entry.Type == TimeEntryType.PauseStart)
                pauseStart = entry;
            else if (entry.Type == TimeEntryType.PauseEnd && pauseStart != null)
            {
                totalPauseMinutes += (decimal)(entry.TimestampUtc - pauseStart.TimestampUtc).TotalMinutes;
                pauseStart = null;
            }
        }

        return totalPauseMinutes;
    }
}
