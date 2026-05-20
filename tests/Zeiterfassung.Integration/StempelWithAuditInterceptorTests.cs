using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;
using Zeiterfassung.Data;
using Zeiterfassung.Data.Interceptors;
using Zeiterfassung.Data.Repositories;

namespace Zeiterfassung.Integration;

/// <summary>
/// Regression: the first stamp from /terminal crashed with
/// "TimeEntry is append-only. Cannot modify/delete entries: 1"
/// because StempelManager uses a two-phase INSERT-then-UPDATE pattern
/// (so the real DB Id is in the hash payload), but AuditInterceptor
/// blocked any Modified state on TimeEntry.
///
/// These tests run StempelManager through the REAL repository and
/// DbContext + AuditInterceptor — covering the surface that the
/// existing FullDayIntegrationTests skipped because they use a
/// hand-rolled InMemoryTimeEntryRepository.
/// </summary>
public class StempelWithAuditInterceptorTests : IDisposable
{
    private readonly ZeiterfassungDbContext _db;
    private readonly TimeEntryRepository _repo;
    private readonly StempelManager _manager;

    public StempelWithAuditInterceptorTests()
    {
        var hashChain = new HashChainService();
        var interceptor = new AuditInterceptor(hashChain);

        var options = new DbContextOptionsBuilder<ZeiterfassungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        _db = new ZeiterfassungDbContext(options);

        _repo    = new TimeEntryRepository(_db);
        _manager = new StempelManager(new StempelService(hashChain), hashChain, new ArbZGValidator());
    }

    public void Dispose() => _db.Dispose();

    private async Task<Employee> SeedEmployeeAsync()
    {
        var emp = new Employee
        {
            FirstName = "Sara", LastName = "Nowak",
            PinHash = "x", PinSalt = "x",
            IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();
        return emp;
    }

    [Fact]
    public async Task FirstStamp_doesNotCrash_andSealsRealHash()
    {
        // Regression for: "TimeEntry is append-only. Cannot modify/delete entries: 1"
        var emp = await SeedEmployeeAsync();

        var act = async () =>
        {
            await _manager.StempelAsync(
                new StempelRequest { EmployeeId = emp.Id, Type = TimeEntryType.Kommen },
                _repo);
        };

        await act.Should().NotThrowAsync(
            "the AuditInterceptor must allow the one-time hash sealing performed by StempelManager");

        var saved = await _db.TimeEntries.SingleAsync();
        saved.Hash.Should().NotBe(HashChainService.GenesisHash, "Hash must be sealed with the real value");
        saved.Hash.Length.Should().Be(64, "SHA-256 hex");
        saved.EmployeeId.Should().Be(emp.Id);
        saved.Type.Should().Be(TimeEntryType.Kommen);
    }

    [Fact]
    public async Task ChainOfFourStamps_allSealCorrectly()
    {
        var emp = await SeedEmployeeAsync();
        var t0 = DateTime.UtcNow.Date.AddHours(8); // start of day, 08:00 UTC

        await _manager.StempelAsync(new StempelRequest
            { EmployeeId = emp.Id, Type = TimeEntryType.Kommen, TimestampOverrideUtc = t0 }, _repo);
        await _manager.StempelAsync(new StempelRequest
            { EmployeeId = emp.Id, Type = TimeEntryType.PauseStart, TimestampOverrideUtc = t0.AddHours(4) }, _repo);
        await _manager.StempelAsync(new StempelRequest
            { EmployeeId = emp.Id, Type = TimeEntryType.PauseEnd, TimestampOverrideUtc = t0.AddHours(4.5) }, _repo);
        await _manager.StempelAsync(new StempelRequest
            { EmployeeId = emp.Id, Type = TimeEntryType.Gehen, TimestampOverrideUtc = t0.AddHours(8.5) }, _repo);

        var entries = await _db.TimeEntries.OrderBy(e => e.Id).ToListAsync();
        entries.Should().HaveCount(4);
        entries.Select(e => e.Hash).Distinct().Should().HaveCount(4, "all hashes must be distinct");
        entries[0].PrevHash.Should().Be(HashChainService.GenesisHash, "first entry chains from genesis");
        for (int i = 1; i < entries.Count; i++)
        {
            entries[i].PrevHash.Should().Be(entries[i - 1].Hash,
                $"entry #{i + 1} must chain to the previous entry's hash");
        }
    }

    [Fact]
    public async Task ManualAttemptToModifyTimestamp_isStillRejected()
    {
        // The append-only guarantee on semantic columns MUST hold.
        var emp = await SeedEmployeeAsync();
        await _manager.StempelAsync(
            new StempelRequest { EmployeeId = emp.Id, Type = TimeEntryType.Kommen },
            _repo);

        var entry = await _db.TimeEntries.SingleAsync();
        entry.TimestampUtc = entry.TimestampUtc.AddHours(-2); // tamper

        var act = async () => await _db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*",
                "modifying anything other than the initial hash sealing must be blocked");
    }

    [Fact]
    public async Task ManualAttemptToReSealHash_isStillRejected()
    {
        // The hash-sealing exemption is ONE-SHOT.
        var emp = await SeedEmployeeAsync();
        await _manager.StempelAsync(
            new StempelRequest { EmployeeId = emp.Id, Type = TimeEntryType.Kommen },
            _repo);

        var entry = await _db.TimeEntries.SingleAsync();
        entry.Hash = new string('a', 64); // attempt to re-seal

        var act = async () => await _db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*bereits versiegelt*",
                "hash sealing is allowed once only");
    }

    [Fact]
    public async Task HashChain_remains_verifiable_after_reread_from_db()
    {
        // Regression: the integrity check used to report false manipulation
        // because SQLite + EF Core returned DateTime values with
        // Kind=Unspecified, and HashChainService.ToUniversalTime() then
        // shifted them by the local offset. The DbContext now pins
        // Kind=Utc via a ValueConverter — verify the chain still matches.
        var emp = await SeedEmployeeAsync();
        var t = DateTime.UtcNow.Date.AddHours(8);
        foreach (var (type, delta) in new[]
                 {
                    (TimeEntryType.Kommen,     0.0),
                    (TimeEntryType.PauseStart, 4.0),
                    (TimeEntryType.PauseEnd,   4.5),
                    (TimeEntryType.Gehen,      8.5),
                 })
        {
            await _manager.StempelAsync(
                new StempelRequest { EmployeeId = emp.Id, Type = type,
                    TimestampOverrideUtc = t.AddHours(delta) },
                _repo);
        }

        // Detach so we get fresh reads from "the database" (in-memory provider).
        foreach (var e in _db.ChangeTracker.Entries().ToList()) e.State = EntityState.Detached;

        var hashChain = new HashChainService();
        var reread = await _db.TimeEntries.OrderBy(e => e.Id).ToListAsync();
        reread.Should().HaveCount(4);

        var prev = HashChainService.GenesisHash;
        foreach (var entry in reread)
        {
            var payload = hashChain.CreatePayload(entry);
            var expected = hashChain.ComputeHash(prev, payload);
            entry.Hash.Should().Be(expected,
                $"recomputed hash for #{entry.Id} must match stored hash after DB round-trip (Kind={entry.TimestampUtc.Kind})");
            entry.TimestampUtc.Kind.Should().Be(DateTimeKind.Utc,
                "EF must restore Kind=Utc on read-back via the global ValueConverter");
            prev = entry.Hash;
        }
    }

    [Fact]
    public async Task GlobalChain_acrossMultipleEmployees_verifies()
    {
        // Regression: TimeEntryRepository.GetPrevHashAsync used to chain
        // per-employee, but HashChainService.VerifyTimeEntryChainAsync
        // verifies the entries as ONE global chain. Mixed-employee data
        // (which is the normal state for any real practice) used to make
        // the integrity check falsely report "Manipulation" starting at
        // the first entry of the second employee. After the fix the
        // chain is global; verification across all employees passes.
        var emp1 = await SeedEmployeeAsync();
        var emp2 = new Employee
        {
            FirstName = "Tobias", LastName = "Köhler",
            PinHash = "x", PinSalt = "x",
            IsActive = true, CreatedAt = DateTime.UtcNow,
        };
        _db.Employees.Add(emp2);
        await _db.SaveChangesAsync();

        var t = DateTime.UtcNow.Date.AddHours(8);
        // Interleave stamps from both employees so the chain is genuinely mixed.
        await _manager.StempelAsync(new StempelRequest { EmployeeId = emp1.Id, Type = TimeEntryType.Kommen,    TimestampOverrideUtc = t.AddMinutes(0)  }, _repo);
        await _manager.StempelAsync(new StempelRequest { EmployeeId = emp2.Id, Type = TimeEntryType.Kommen,    TimestampOverrideUtc = t.AddMinutes(5)  }, _repo);
        await _manager.StempelAsync(new StempelRequest { EmployeeId = emp1.Id, Type = TimeEntryType.PauseStart,TimestampOverrideUtc = t.AddHours(4)    }, _repo);
        await _manager.StempelAsync(new StempelRequest { EmployeeId = emp2.Id, Type = TimeEntryType.PauseStart,TimestampOverrideUtc = t.AddHours(4.1)  }, _repo);
        await _manager.StempelAsync(new StempelRequest { EmployeeId = emp1.Id, Type = TimeEntryType.PauseEnd,  TimestampOverrideUtc = t.AddHours(4.5)  }, _repo);
        await _manager.StempelAsync(new StempelRequest { EmployeeId = emp2.Id, Type = TimeEntryType.PauseEnd,  TimestampOverrideUtc = t.AddHours(4.6)  }, _repo);
        await _manager.StempelAsync(new StempelRequest { EmployeeId = emp1.Id, Type = TimeEntryType.Gehen,     TimestampOverrideUtc = t.AddHours(8.5)  }, _repo);
        await _manager.StempelAsync(new StempelRequest { EmployeeId = emp2.Id, Type = TimeEntryType.Gehen,     TimestampOverrideUtc = t.AddHours(8.6)  }, _repo);

        // Force a clean re-read so the verification works against stored
        // values, not in-memory tracked entities.
        foreach (var e in _db.ChangeTracker.Entries().ToList()) e.State = EntityState.Detached;

        var entries = await _db.TimeEntries.OrderBy(e => e.Id).ToListAsync();
        entries.Should().HaveCount(8);

        var verifier = new HashChainService();
        var result = await verifier.VerifyTimeEntryChainAsync(entries);

        result.IsValid.Should().BeTrue(
            $"global hash chain must verify across employees " +
            $"(failed at #{result.FailedEntryId}: expected {result.ExpectedHash} got {result.ActualHash})");
    }

    [Fact]
    public async Task AuditLogChain_remainsVerifiable_acrossSeveralOperations()
    {
        // Regression: AuditInterceptor computes the AuditLog hash inside
        // SavingChangesAsync — Id is still 0 at that point. After save
        // EF assigns the auto-increment Id. Including Id in the payload
        // made the chain always fail verification. With Id removed from
        // CreatePayload(AuditLog), verification must pass after multiple
        // audited mutations.
        var emp = await SeedEmployeeAsync();
        emp.Email = "first@example.com";
        await _db.SaveChangesAsync();
        emp.Email = "second@example.com";
        await _db.SaveChangesAsync();
        emp.IsActive = false;
        await _db.SaveChangesAsync();

        foreach (var e in _db.ChangeTracker.Entries().ToList()) e.State = EntityState.Detached;
        var logs = await _db.AuditLogs.OrderBy(a => a.Id).ToListAsync();
        logs.Should().HaveCountGreaterThanOrEqualTo(3);

        var verifier = new HashChainService();
        var result = await verifier.VerifyAuditLogChainAsync(logs);

        result.IsValid.Should().BeTrue(
            $"AuditLog chain must verify (failed at #{result.FailedEntryId}: " +
            $"expected {result.ExpectedHash}, actual {result.ActualHash})");
    }

    [Fact]
    public async Task DeletingATimeEntry_isStillRejected()
    {
        var emp = await SeedEmployeeAsync();
        await _manager.StempelAsync(
            new StempelRequest { EmployeeId = emp.Id, Type = TimeEntryType.Kommen },
            _repo);

        var entry = await _db.TimeEntries.SingleAsync();
        _db.TimeEntries.Remove(entry);

        var act = async () => await _db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nicht gelöscht*");
    }
}
