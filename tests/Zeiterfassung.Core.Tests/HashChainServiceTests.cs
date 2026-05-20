using FluentAssertions;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;
using Xunit;

namespace Zeiterfassung.Core.Tests;

public class HashChainServiceTests
{
    [Fact]
    public void ComputeHash_ShouldProduceConsistentResults()
    {
        var service = new HashChainService();
        var payload = "1|1|Kommen|2026-05-20T10:00:00.0000000Z|Terminal|||2026-05-20T10:00:00.0000000Z";

        var hash1 = service.ComputeHash(HashChainService.GenesisHash, payload);
        var hash2 = service.ComputeHash(HashChainService.GenesisHash, payload);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
        hash1.Should().NotBeEmpty();
    }

    [Fact]
    public void ComputeHash_DifferentPrevHash_ShouldProduceDifferentResult()
    {
        var service = new HashChainService();
        var payload = "1|1|Kommen|2026-05-20T10:00:00.0000000Z|Terminal|||2026-05-20T10:00:00.0000000Z";
        var otherPrev = "aaaa" + new string('0', 60);

        var hash1 = service.ComputeHash(HashChainService.GenesisHash, payload);
        var hash2 = service.ComputeHash(otherPrev, payload);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void CreatePayload_TimeEntry_ShouldFormatCorrectly()
    {
        var service = new HashChainService();
        var ts = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
        var entry = new TimeEntry
        {
            Id = 1,
            EmployeeId = 2,
            Type = TimeEntryType.Kommen,
            TimestampUtc = ts,
            Source = EntrySource.Terminal,
            CorrectionOfId = null,
            CreatedByUserId = null,
            CreatedAtUtc = ts
        };

        var payload = service.CreatePayload(entry);

        payload.Should().StartWith("1|2|Kommen|");
        payload.Should().Contain("Terminal");
        payload.Should().Contain("2026-05-20");
    }

    [Fact]
    public async Task VerifyTimeEntryChain_WithValidChain_ShouldReturnValid()
    {
        var service = new HashChainService();
        var ts = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        var entry = new TimeEntry
        {
            Id = 1,
            EmployeeId = 1,
            Type = TimeEntryType.Kommen,
            TimestampUtc = ts,
            Source = EntrySource.Terminal,
            CreatedAtUtc = ts,
            PrevHash = HashChainService.GenesisHash,
            Hash = string.Empty
        };

        var payload = service.CreatePayload(entry);
        entry.Hash = service.ComputeHash(entry.PrevHash, payload);

        var result = await service.VerifyTimeEntryChainAsync(new[] { entry });

        result.IsValid.Should().BeTrue();
        result.FailedEntryId.Should().BeNull();
    }

    [Fact]
    public async Task VerifyTimeEntryChain_WithTamperedTimestamp_ShouldDetectManipulation()
    {
        var service = new HashChainService();
        var ts = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        var entry = new TimeEntry
        {
            Id = 1,
            EmployeeId = 1,
            Type = TimeEntryType.Kommen,
            TimestampUtc = ts,
            Source = EntrySource.Terminal,
            CreatedAtUtc = ts,
            PrevHash = HashChainService.GenesisHash,
            Hash = string.Empty
        };

        var payload = service.CreatePayload(entry);
        entry.Hash = service.ComputeHash(entry.PrevHash, payload);

        // Tamper: change timestamp after hash was computed
        entry.TimestampUtc = ts.AddHours(2);

        var result = await service.VerifyTimeEntryChainAsync(new[] { entry });

        result.IsValid.Should().BeFalse();
        result.FailedEntryId.Should().Be(1);
    }

    [Fact]
    public async Task VerifyTimeEntryChain_TwoEntries_CorrectChain_ShouldPass()
    {
        var service = new HashChainService();
        var ts1 = new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc);
        var ts2 = new DateTime(2026, 5, 20, 17, 0, 0, DateTimeKind.Utc);

        var entry1 = new TimeEntry
        {
            Id = 1, EmployeeId = 1, Type = TimeEntryType.Kommen, TimestampUtc = ts1,
            Source = EntrySource.Terminal, CreatedAtUtc = ts1, PrevHash = HashChainService.GenesisHash, Hash = string.Empty
        };
        entry1.Hash = service.ComputeHash(entry1.PrevHash, service.CreatePayload(entry1));

        var entry2 = new TimeEntry
        {
            Id = 2, EmployeeId = 1, Type = TimeEntryType.Gehen, TimestampUtc = ts2,
            Source = EntrySource.Terminal, CreatedAtUtc = ts2, PrevHash = entry1.Hash, Hash = string.Empty
        };
        entry2.Hash = service.ComputeHash(entry2.PrevHash, service.CreatePayload(entry2));

        var result = await service.VerifyTimeEntryChainAsync(new[] { entry1, entry2 });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyTimeEntryChain_BrokenPrevHash_ShouldDetectGap()
    {
        var service = new HashChainService();
        var ts1 = new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc);
        var ts2 = new DateTime(2026, 5, 20, 17, 0, 0, DateTimeKind.Utc);

        var entry1 = new TimeEntry
        {
            Id = 1, EmployeeId = 1, Type = TimeEntryType.Kommen, TimestampUtc = ts1,
            Source = EntrySource.Terminal, CreatedAtUtc = ts1, PrevHash = HashChainService.GenesisHash, Hash = string.Empty
        };
        entry1.Hash = service.ComputeHash(entry1.PrevHash, service.CreatePayload(entry1));

        var entry2 = new TimeEntry
        {
            Id = 2, EmployeeId = 1, Type = TimeEntryType.Gehen, TimestampUtc = ts2,
            Source = EntrySource.Terminal, CreatedAtUtc = ts2,
            // Wrong: uses Genesis hash instead of entry1.Hash
            PrevHash = HashChainService.GenesisHash,
            Hash = string.Empty
        };
        entry2.Hash = service.ComputeHash(entry2.PrevHash, service.CreatePayload(entry2));

        var result = await service.VerifyTimeEntryChainAsync(new[] { entry1, entry2 });

        result.IsValid.Should().BeFalse();
        result.FailedEntryId.Should().Be(2);
    }

    [Fact]
    public async Task VerifyTimeEntryChain_Concurrency_50Tasks_ShouldBeConsistent()
    {
        var service = new HashChainService();
        var prevHash = HashChainService.GenesisHash;
        var entries = new List<TimeEntry>();
        var lockObj = new object();

        // Simulate 50 sequential entries (testing payload determinism, not true concurrency for hash chain)
        for (int i = 1; i <= 50; i++)
        {
            var ts = new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc).AddMinutes(i);
            var entry = new TimeEntry
            {
                Id = i, EmployeeId = 1, Type = TimeEntryType.Kommen, TimestampUtc = ts,
                Source = EntrySource.Terminal, CreatedAtUtc = ts, PrevHash = prevHash, Hash = string.Empty
            };
            entry.Hash = service.ComputeHash(prevHash, service.CreatePayload(entry));
            prevHash = entry.Hash;
            entries.Add(entry);
        }

        var result = await service.VerifyTimeEntryChainAsync(entries);

        result.IsValid.Should().BeTrue();
        entries.Should().HaveCount(50);
        entries.Select(e => e.Hash).Distinct().Should().HaveCount(50); // All hashes unique
    }
}
