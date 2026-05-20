using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class StempelService
{
    private readonly HashChainService _hashChainService;

    public StempelService(HashChainService hashChainService)
    {
        _hashChainService = hashChainService;
    }

    public StempelValidationResult ValidateEntry(
        TimeEntryType type,
        IList<TimeEntry> employeeEntries)
    {
        var lastEntry = employeeEntries.OrderByDescending(e => e.CreatedAtUtc).FirstOrDefault();

        if (lastEntry == null)
        {
            if (type != TimeEntryType.Kommen)
            {
                return new StempelValidationResult
                {
                    IsValid = false,
                    Error = "First entry must be 'Kommen'"
                };
            }
            return new StempelValidationResult { IsValid = true };
        }

        return (lastEntry.Type, type) switch
        {
            (TimeEntryType.Kommen, TimeEntryType.Kommen) => new StempelValidationResult
            {
                IsValid = false,
                Error = "Cannot stamp 'Kommen' twice in a row"
            },
            (TimeEntryType.Gehen, TimeEntryType.Gehen) => new StempelValidationResult
            {
                IsValid = false,
                Error = "Cannot stamp 'Gehen' without 'Kommen' first"
            },
            (TimeEntryType.PauseStart, TimeEntryType.Kommen) => new StempelValidationResult
            {
                IsValid = false,
                Error = "Cannot stamp 'Kommen' while pause is active"
            },
            (TimeEntryType.PauseStart, TimeEntryType.PauseStart) => new StempelValidationResult
            {
                IsValid = false,
                Error = "Cannot start pause twice"
            },
            (TimeEntryType.PauseStart, TimeEntryType.PauseEnd) => new StempelValidationResult
            {
                IsValid = true
            },
            (TimeEntryType.PauseEnd, TimeEntryType.PauseEnd) => new StempelValidationResult
            {
                IsValid = false,
                Error = "Cannot end pause twice"
            },
            (TimeEntryType.PauseEnd, TimeEntryType.PauseStart) => new StempelValidationResult
            {
                IsValid = true
            },
            _ => new StempelValidationResult { IsValid = true }
        };
    }

    public bool IsDuplicateEntry(
        TimeEntryType type,
        DateTime timestamp,
        IList<TimeEntry> employeeEntries,
        int duplicateThresholdSeconds = 5)
    {
        var recentEntry = employeeEntries
            .Where(e => e.Type == type)
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefault();

        if (recentEntry == null)
            return false;

        var timeDiff = (timestamp - recentEntry.TimestampUtc).TotalSeconds;
        return timeDiff < duplicateThresholdSeconds && timeDiff >= 0;
    }

    public ArbZGWarning? GetArbZGWarning(
        TimeEntryType type,
        IList<TimeEntry> employeeEntries,
        decimal maxDailyHours = 10)
    {
        if (type != TimeEntryType.Gehen)
            return null;

        var today = DateTime.Today;
        var todayEntries = employeeEntries
            .Where(e => e.TimestampUtc.Date == today)
            .OrderBy(e => e.TimestampUtc)
            .ToList();

        if (todayEntries.Count == 0)
            return null;

        var kommenEntry = todayEntries.FirstOrDefault(e => e.Type == TimeEntryType.Kommen);
        if (kommenEntry == null)
            return null;

        var gehenEntry = todayEntries.LastOrDefault(e => e.Type == TimeEntryType.Gehen) ??
            new TimeEntry { TimestampUtc = DateTime.UtcNow };

        var dailyHours = (gehenEntry.TimestampUtc - kommenEntry.TimestampUtc).TotalHours;
        if ((decimal)dailyHours > maxDailyHours)
        {
            return new ArbZGWarning
            {
                Type = "DailyHoursExceeded",
                Message = $"Daily working time ({dailyHours:F1}h) exceeds {maxDailyHours}h"
            };
        }

        var pauseDuration = CalculatePauseDuration(todayEntries);
        if (dailyHours > 6 && pauseDuration < 30)
        {
            return new ArbZGWarning
            {
                Type = "InsufficientPause",
                Message = $"Insufficient pause ({pauseDuration}min) for {dailyHours:F1}h work"
            };
        }

        return null;
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

public class StempelValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
}

public class ArbZGWarning
{
    public string Type { get; set; } = null!;
    public string Message { get; set; } = null!;
}
