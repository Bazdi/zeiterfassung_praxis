using FluentAssertions;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;
using Xunit;

namespace Zeiterfassung.Core.Tests;

public class ArbZGValidatorTests
{
    private static TimeEntry MakeEntry(TimeEntryType type, DateTime utc) => new()
    {
        Id = 0, EmployeeId = 1, Type = type, TimestampUtc = utc,
        Source = EntrySource.Terminal, CreatedAtUtc = utc, PrevHash = "", Hash = ""
    };

    [Fact]
    public void ValidateDay_TenHoursWork_ShouldWarn()
    {
        var validator = new ArbZGValidator();
        var date = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var entries = new List<TimeEntry>
        {
            MakeEntry(TimeEntryType.Kommen, date.AddHours(7)),
            MakeEntry(TimeEntryType.PauseStart, date.AddHours(12)),
            MakeEntry(TimeEntryType.PauseEnd, date.AddHours(12).AddMinutes(45)),
            MakeEntry(TimeEntryType.Gehen, date.AddHours(18).AddMinutes(1)) // 10h01m net
        };

        var warnings = validator.ValidateDay(date, entries);

        warnings.Should().Contain(w => w.Type == "DailyHoursExceeded");
    }

    [Fact]
    public void ValidateDay_NormalDay_ShouldHaveNoWarnings()
    {
        var validator = new ArbZGValidator();
        var date = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var entries = new List<TimeEntry>
        {
            MakeEntry(TimeEntryType.Kommen, date.AddHours(8)),
            MakeEntry(TimeEntryType.PauseStart, date.AddHours(12)),
            MakeEntry(TimeEntryType.PauseEnd, date.AddHours(12).AddMinutes(30)),
            MakeEntry(TimeEntryType.Gehen, date.AddHours(16))
        };

        var warnings = validator.ValidateDay(date, entries);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDay_SixHoursNoPause_ShouldWarnAboutInsufficientPause()
    {
        var validator = new ArbZGValidator();
        var date = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var entries = new List<TimeEntry>
        {
            MakeEntry(TimeEntryType.Kommen, date.AddHours(8)),
            MakeEntry(TimeEntryType.Gehen, date.AddHours(14).AddMinutes(5)) // 6h05m no pause
        };

        var warnings = validator.ValidateDay(date, entries);

        warnings.Should().Contain(w => w.Type == "InsufficientPause");
    }

    [Fact]
    public void ValidateDay_NineHoursOnlyThirtyMinPause_ShouldWarn()
    {
        var validator = new ArbZGValidator();
        var date = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var entries = new List<TimeEntry>
        {
            MakeEntry(TimeEntryType.Kommen, date.AddHours(7)),
            MakeEntry(TimeEntryType.PauseStart, date.AddHours(12)),
            MakeEntry(TimeEntryType.PauseEnd, date.AddHours(12).AddMinutes(30)), // 30 min only
            MakeEntry(TimeEntryType.Gehen, date.AddHours(16).AddMinutes(31)) // 9h01m net
        };

        var warnings = validator.ValidateDay(date, entries);

        warnings.Should().Contain(w => w.Type == "InsufficientPause");
    }

    [Fact]
    public void ValidateDay_InsufficientRestFromPreviousDay_ShouldWarn()
    {
        var validator = new ArbZGValidator();
        var today = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var prevDayGehen = new DateTime(2026, 5, 19, 22, 30, 0, DateTimeKind.Utc); // 22:30 previous day
        var todayKommen = today.AddHours(8); // 08:00 today = only 9.5h rest

        var entries = new List<TimeEntry>
        {
            MakeEntry(TimeEntryType.Kommen, todayKommen),
            MakeEntry(TimeEntryType.Gehen, todayKommen.AddHours(8))
        };

        var warnings = validator.ValidateDay(today, entries, prevDayGehen);

        warnings.Should().Contain(w => w.Type == "InsufficientRestPeriod");
    }

    [Fact]
    public void ValidateDay_SufficientRestFromPreviousDay_ShouldNotWarn()
    {
        var validator = new ArbZGValidator();
        var today = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var prevDayGehen = new DateTime(2026, 5, 19, 18, 0, 0, DateTimeKind.Utc); // 18:00 previous day
        var todayKommen = today.AddHours(8); // 08:00 today = 14h rest

        var entries = new List<TimeEntry>
        {
            MakeEntry(TimeEntryType.Kommen, todayKommen),
            MakeEntry(TimeEntryType.Gehen, todayKommen.AddHours(8))
        };

        var warnings = validator.ValidateDay(today, entries, prevDayGehen);

        warnings.Should().NotContain(w => w.Type == "InsufficientRestPeriod");
    }
}
