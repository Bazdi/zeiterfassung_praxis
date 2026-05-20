using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;

namespace Zeiterfassung.Data.Repositories;

public class TimeEntryRepository : ITimeEntryRepository
{
    private readonly ZeiterfassungDbContext _db;

    public TimeEntryRepository(ZeiterfassungDbContext db)
    {
        _db = db;
    }

    public async Task<List<TimeEntry>> GetByEmployeeAsync(
        int employeeId,
        DateTime from,
        DateTime to)
    {
        return await _db.TimeEntries
            .Where(e => e.EmployeeId == employeeId
                && e.TimestampUtc >= from
                && e.TimestampUtc <= to)
            .OrderBy(e => e.TimestampUtc)
            .ToListAsync();
    }

    public async Task<string?> GetLastHashAsync(int employeeId)
    {
        var lastEntry = await _db.TimeEntries
            .Where(e => e.EmployeeId == employeeId)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();
        return lastEntry?.Hash;
    }

    public async Task AddAsync(TimeEntry entry)
    {
        _db.TimeEntries.Add(entry);
    }

    public async Task SaveAsync()
    {
        await _db.SaveChangesAsync();
    }
}
