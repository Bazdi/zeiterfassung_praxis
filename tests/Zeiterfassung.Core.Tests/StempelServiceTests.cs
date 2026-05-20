using FluentAssertions;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;
using Xunit;

namespace Zeiterfassung.Core.Tests;

public class StempelServiceTests
{
    [Fact]
    public void ValidateEntry_FirstEntryShouldBeKommen()
    {
        var service = new StempelService(new HashChainService());
        var entries = new List<TimeEntry>();

        var result = service.ValidateEntry(TimeEntryType.Kommen, entries);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateEntry_FirstEntryCantBeGehen()
    {
        var service = new StempelService(new HashChainService());
        var entries = new List<TimeEntry>();

        var result = service.ValidateEntry(TimeEntryType.Gehen, entries);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Kommen");
    }

    [Fact]
    public void ValidateEntry_CantHaveDoubleKommen()
    {
        var service = new StempelService(new HashChainService());
        var entries = new List<TimeEntry>
        {
            new TimeEntry
            {
                Id = 1,
                EmployeeId = 1,
                Type = TimeEntryType.Kommen,
                TimestampUtc = DateTime.UtcNow,
                Source = EntrySource.Terminal,
                CreatedAtUtc = DateTime.UtcNow,
                PrevHash = "",
                Hash = ""
            }
        };

        var result = service.ValidateEntry(TimeEntryType.Kommen, entries);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateEntry_CanTransitionFromKommenToGehen()
    {
        var service = new StempelService(new HashChainService());
        var entries = new List<TimeEntry>
        {
            new TimeEntry
            {
                Id = 1,
                EmployeeId = 1,
                Type = TimeEntryType.Kommen,
                TimestampUtc = DateTime.UtcNow,
                Source = EntrySource.Terminal,
                CreatedAtUtc = DateTime.UtcNow,
                PrevHash = "",
                Hash = ""
            }
        };

        var result = service.ValidateEntry(TimeEntryType.Gehen, entries);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateEntry_CanTransitionFromKommenToPauseStart()
    {
        var service = new StempelService(new HashChainService());
        var entries = new List<TimeEntry>
        {
            new TimeEntry
            {
                Id = 1,
                EmployeeId = 1,
                Type = TimeEntryType.Kommen,
                TimestampUtc = DateTime.UtcNow,
                Source = EntrySource.Terminal,
                CreatedAtUtc = DateTime.UtcNow,
                PrevHash = "",
                Hash = ""
            }
        };

        var result = service.ValidateEntry(TimeEntryType.PauseStart, entries);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsDuplicateEntry_WithinThreshold_ShouldBeDetected()
    {
        var service = new StempelService(new HashChainService());
        var now = DateTime.UtcNow;
        var entries = new List<TimeEntry>
        {
            new TimeEntry
            {
                Id = 1,
                EmployeeId = 1,
                Type = TimeEntryType.Kommen,
                TimestampUtc = now.AddSeconds(-2),
                Source = EntrySource.Terminal,
                CreatedAtUtc = now,
                PrevHash = "",
                Hash = ""
            }
        };

        var isDuplicate = service.IsDuplicateEntry(TimeEntryType.Kommen, now, entries);

        isDuplicate.Should().BeTrue();
    }

    [Fact]
    public void IsDuplicateEntry_OutsideThreshold_ShouldNotBeDetected()
    {
        var service = new StempelService(new HashChainService());
        var now = DateTime.UtcNow;
        var entries = new List<TimeEntry>
        {
            new TimeEntry
            {
                Id = 1,
                EmployeeId = 1,
                Type = TimeEntryType.Kommen,
                TimestampUtc = now.AddSeconds(-10),
                Source = EntrySource.Terminal,
                CreatedAtUtc = now,
                PrevHash = "",
                Hash = ""
            }
        };

        var isDuplicate = service.IsDuplicateEntry(TimeEntryType.Kommen, now, entries);

        isDuplicate.Should().BeFalse();
    }
}
