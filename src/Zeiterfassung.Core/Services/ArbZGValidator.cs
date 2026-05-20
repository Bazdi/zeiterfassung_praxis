using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class ArbZGValidator
{
    public IList<ValidationWarning> ValidateDay(
        DateTime date,
        IList<TimeEntry> dayEntries)
    {
        var warnings = new List<ValidationWarning>();
        var sortedEntries = dayEntries.OrderBy(e => e.TimestampUtc).ToList();

        if (sortedEntries.Count == 0)
            return warnings;

        var kommenEntry = sortedEntries.FirstOrDefault(e => e.Type == TimeEntryType.Kommen);
        var gehenEntry = sortedEntries.LastOrDefault(e => e.Type == TimeEntryType.Gehen);

        if (kommenEntry == null || gehenEntry == null)
            return warnings;

        var workDuration = gehenEntry.TimestampUtc - kommenEntry.TimestampUtc;
        var pauseDuration = CalculatePauseDuration(sortedEntries);
        var netWorkDuration = workDuration.TotalMinutes - pauseDuration;

        if (netWorkDuration > 600)
        {
            warnings.Add(new ValidationWarning
            {
                Type = "ExcessiveWorkingTime",
                Message = $"Working time exceeds 10 hours ({netWorkDuration / 60:F1}h)",
                Severity = WarningSeverity.High
            });
        }

        if (netWorkDuration > 360 && pauseDuration < 30)
        {
            warnings.Add(new ValidationWarning
            {
                Type = "InsufficientPause",
                Message = $"Working {netWorkDuration / 60:F1}h with only {pauseDuration}min pause",
                Severity = WarningSeverity.High
            });
        }

        if (netWorkDuration > 540 && pauseDuration < 45)
        {
            warnings.Add(new ValidationWarning
            {
                Type = "InsufficientPause",
                Message = $"Working {netWorkDuration / 60:F1}h with only {pauseDuration}min pause",
                Severity = WarningSeverity.High
            });
        }

        if (pauseDuration < 15 && netWorkDuration > 360)
        {
            warnings.Add(new ValidationWarning
            {
                Type = "MinimalPause",
                Message = "No break or insufficient break detected",
                Severity = WarningSeverity.Medium
            });
        }

        var lastGehenTime = gehenEntry.TimestampUtc;
        var nextKommenTime = GetNextKommenTime(dayEntries, gehenEntry);
        if (nextKommenTime.HasValue)
        {
            var restDuration = (nextKommenTime.Value - lastGehenTime).TotalHours;
            if (restDuration < 11)
            {
                warnings.Add(new ValidationWarning
                {
                    Type = "InsufficientRestPeriod",
                    Message = $"Less than 11 hours rest ({restDuration:F1}h) until next work day",
                    Severity = WarningSeverity.High
                });
            }
        }

        return warnings;
    }

    private DateTime? GetNextKommenTime(IList<TimeEntry> entries, TimeEntry gehenEntry)
    {
        return entries
            .Where(e => e.Type == TimeEntryType.Kommen && e.TimestampUtc > gehenEntry.TimestampUtc)
            .OrderBy(e => e.TimestampUtc)
            .FirstOrDefault()
            ?.TimestampUtc;
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
