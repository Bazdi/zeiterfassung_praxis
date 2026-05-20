using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class ArbZGValidator
{
    private const int MinPauseFor6HoursWork = 30;
    private const int MinPauseFor9HoursWork = 45;
    private const double MaxDailyWorkHours = 10.0;
    private const double MinRestHoursBetweenDays = 11.0;

    /// <summary>
    /// Validates a day's time entries against ArbZG rules.
    /// Pass the previous day's Gehen time via prevDayGehen to check 11h rest rule.
    /// </summary>
    public IList<ValidationWarning> ValidateDay(
        DateTime date,
        IList<TimeEntry> dayEntries,
        DateTime? prevDayGehenUtc = null)
    {
        var warnings = new List<ValidationWarning>();
        var sortedEntries = dayEntries.OrderBy(e => e.TimestampUtc).ToList();

        if (sortedEntries.Count == 0)
            return warnings;

        var kommenEntry = sortedEntries.FirstOrDefault(e => e.Type == TimeEntryType.Kommen);
        var gehenEntry = sortedEntries.LastOrDefault(e => e.Type == TimeEntryType.Gehen);

        if (kommenEntry == null || gehenEntry == null)
            return warnings;

        var workDurationMinutes = (gehenEntry.TimestampUtc - kommenEntry.TimestampUtc).TotalMinutes;
        var pauseMinutes = CalculatePauseMinutes(sortedEntries);
        var netWorkMinutes = workDurationMinutes - pauseMinutes;
        var netWorkHours = netWorkMinutes / 60.0;

        // Rule: max 10 hours net work
        if (netWorkHours > MaxDailyWorkHours)
        {
            warnings.Add(new ValidationWarning
            {
                Type = "DailyHoursExceeded",
                Message = $"Tägliche Arbeitszeit von {MaxDailyWorkHours}h überschritten: {netWorkHours:F1}h",
                Severity = WarningSeverity.High
            });
        }

        // Rule: at least 30 min break after 6h, 45 min after 9h (§ 4 ArbZG)
        if (netWorkMinutes > 540 && pauseMinutes < MinPauseFor9HoursWork)
        {
            warnings.Add(new ValidationWarning
            {
                Type = "InsufficientPause",
                Message = $"Mehr als 9h Arbeit ({netWorkHours:F1}h), aber nur {(int)pauseMinutes}min Pause (min. 45 min erforderlich)",
                Severity = WarningSeverity.High
            });
        }
        else if (netWorkMinutes > 360 && pauseMinutes < MinPauseFor6HoursWork)
        {
            warnings.Add(new ValidationWarning
            {
                Type = "InsufficientPause",
                Message = $"Mehr als 6h Arbeit ({netWorkHours:F1}h), aber nur {(int)pauseMinutes}min Pause (min. 30 min erforderlich)",
                Severity = WarningSeverity.High
            });
        }

        // Rule: 11h rest between days (§ 5 ArbZG)
        if (prevDayGehenUtc.HasValue)
        {
            var restHours = (kommenEntry.TimestampUtc - prevDayGehenUtc.Value).TotalHours;
            if (restHours < MinRestHoursBetweenDays)
            {
                warnings.Add(new ValidationWarning
                {
                    Type = "InsufficientRestPeriod",
                    Message = $"Ruhezeit von {MinRestHoursBetweenDays}h unterschritten: nur {restHours:F1}h Ruhe vor diesem Arbeitstag",
                    Severity = WarningSeverity.High
                });
            }
        }

        return warnings;
    }

    /// <summary>
    /// Live-check before a stamp action: returns immediate ArbZG warnings.
    /// </summary>
    public ValidationWarning? GetLiveWarning(
        TimeEntryType stampType,
        IList<TimeEntry> todayEntries,
        DateTime? prevDayGehenUtc = null)
    {
        var sorted = todayEntries.OrderBy(e => e.TimestampUtc).ToList();

        if (stampType == TimeEntryType.Kommen && prevDayGehenUtc.HasValue)
        {
            var restHours = (DateTime.UtcNow - prevDayGehenUtc.Value).TotalHours;
            if (restHours < MinRestHoursBetweenDays)
            {
                return new ValidationWarning
                {
                    Type = "InsufficientRestPeriod",
                    Message = $"Achtung: Nur {restHours:F1}h seit letztem Arbeitsende (11h empfohlen)",
                    Severity = WarningSeverity.High
                };
            }
        }

        if (stampType == TimeEntryType.PauseStart || stampType == TimeEntryType.Gehen)
        {
            var kommenEntry = sorted.FirstOrDefault(e => e.Type == TimeEntryType.Kommen);
            if (kommenEntry != null)
            {
                var workMinutesSoFar = (DateTime.UtcNow - kommenEntry.TimestampUtc).TotalMinutes
                    - CalculatePauseMinutes(sorted);

                if (workMinutesSoFar > 360 && stampType == TimeEntryType.PauseStart)
                {
                    return new ValidationWarning
                    {
                        Type = "PauseRequired",
                        Message = "Mehr als 6h Arbeit — jetzt Pause machen!",
                        Severity = WarningSeverity.Medium
                    };
                }
            }
        }

        return null;
    }

    private static double CalculatePauseMinutes(IList<TimeEntry> dayEntries)
    {
        double totalPauseMinutes = 0;
        TimeEntry? pauseStart = null;

        foreach (var entry in dayEntries)
        {
            if (entry.Type == TimeEntryType.PauseStart)
                pauseStart = entry;
            else if (entry.Type == TimeEntryType.PauseEnd && pauseStart != null)
            {
                totalPauseMinutes += (entry.TimestampUtc - pauseStart.TimestampUtc).TotalMinutes;
                pauseStart = null;
            }
        }

        return totalPauseMinutes;
    }
}

public class ValidationWarning
{
    public string Type { get; set; } = null!;
    public string Message { get; set; } = null!;
    public WarningSeverity Severity { get; set; }
}

public enum WarningSeverity
{
    Low,
    Medium,
    High
}
