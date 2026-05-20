using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;

namespace Zeiterfassung.Data.Repositories;

public class TimeEntryRepository : ITimeEntryRepository
{
    private readonly ZeiterfassungDbContext _db;

    public TimeEntryRepository(ZeiterfassungDbContext db) => _db = db;

    public async Task<List<TimeEntry>> GetByEmployeeAsync(int employeeId, DateTime from, DateTime to)
        => await _db.TimeEntries
            .Where(e => e.EmployeeId == employeeId && e.TimestampUtc >= from && e.TimestampUtc <= to)
            .OrderBy(e => e.TimestampUtc)
            .ToListAsync();

    public async Task<string> GetPrevHashAsync(long entryId, int employeeId)
    {
        // GoBD: one global chain across ALL employees — every stamp chains
        // to the previous one no matter who made it. Per-employee chaining
        // would let an attacker insert/swap entries for one employee
        // without breaking that employee's chain, and would also confuse
        // HashChainService.VerifyTimeEntryChainAsync (which iterates the
        // entire ordered list as a single chain). The employeeId
        // parameter is kept for API stability but intentionally ignored.
        _ = employeeId;
        var prev = await _db.TimeEntries
            .Where(e => e.Id < entryId)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();
        return prev?.Hash ?? HashChainService.GenesisHash;
    }

    public async Task AddAndSaveAsync(TimeEntry entry)
    {
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(); // entry.Id is set after this
    }

    public async Task UpdateHashAsync(TimeEntry entry, string prevHash, string hash)
    {
        entry.PrevHash = prevHash;
        entry.Hash = hash;
        await _db.SaveChangesAsync();
    }

    public Task AddAsync(TimeEntry entry) { _db.TimeEntries.Add(entry); return Task.CompletedTask; }
    public async Task SaveAsync() => await _db.SaveChangesAsync();
}
