using FluentAssertions;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;
using Xunit;

namespace Zeiterfassung.Core.Tests;

public class HashChainServiceTests
{
    [Fact]
    public async Task ComputeHash_ShouldProduceConsistentResults()
    {
        var service = new HashChainService();
        var payload = "test|1|1|2026-05-20T10:00:00Z|Terminal||1|2026-05-20T10:00:00Z";

        var hash1 = await service.ComputeHashAsync("0000000000000000000000000000000000000000000000000000000000000000", payload);
        var hash2 = await service.ComputeHashAsync("0000000000000000000000000000000000000000000000000000000000000000", payload);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
        hash1.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreatePayload_TimeEntry_ShouldFormatCorrectly()
    {
        var service = new HashChainService();
        var entry = new TimeEntry
        {
            Id = 1,
            EmployeeId = 2,
            Type = TimeEntryType.Kommen,
            TimestampUtc = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc),
            Source = EntrySource.Terminal,
            CorrectionOfId = null,
            CreatedByUserId = null,
            CreatedAtUtc = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc)
        };

        var payload = service.CreatePayload(entry);

        payload.Should().Contain("|");
        payload.Should().StartWith("1|");
        payload.Should().Contain("Kommen");
        payload.Should().Contain("Terminal");
    }

    [Fact]
    public async Task VerifyChain_WithValidChain_ShouldReturnValid()
    {
        var service = new HashChainService();
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
                PrevHash = "0000000000000000000000000000000000000000000000000000000000000000",
                Hash = "" // Will be computed below
            }
        };

        var payload = service.CreatePayload(entries[0]);
        entries[0].Hash = await service.ComputeHashAsync(entries[0].PrevHash, payload);

        var result = await service.VerifyChainAsync(entries);

        result.IsValid.Should().BeTrue();
        result.FailedEntryId.Should().BeNull();
    }

    [Fact]
    public async Task VerifyChain_WithTamperedEntry_ShouldDetectManipulation()
    {
        var service = new HashChainService();
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
                PrevHash = "0000000000000000000000000000000000000000000000000000000000000000",
                Hash = "" // Will be computed below
            }
        };

        var payload = service.CreatePayload(entries[0]);
        entries[0].Hash = await service.ComputeHashAsync(entries[0].PrevHash, payload);

        // Tamper with the entry
        entries[0].TimestampUtc = entries[0].TimestampUtc.AddHours(1);

        var result = await service.VerifyChainAsync(entries);

        result.IsValid.Should().BeFalse();
        result.FailedEntryId.Should().Be(1);
    }
}
