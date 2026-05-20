using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

/// <summary>
/// Orchestrates all stamping operations: validates sequence, chains hash, persists.
/// The hash is computed AFTER the DB INSERT so the real auto-increment ID is in the payload.
/// </summary>
public class StempelManager
{
    private readonly StempelService _stempelService;
    private readonly HashChainService _hashChainService;
    private readonly ArbZGValidator _arbZGValidator;

    public StempelManager(
        StempelService stempelService,
        HashChainService hashChainService,
        ArbZGValidator arbZGValidator)
    {
        _stempelService = stempelService;
        _hashChainService = hashChainService;
        _arbZGValidator = arbZGValidator;
    }

    /// <summary>
    /// Creates and persists a TimeEntry.
    /// Hash is computed after INSERT so the real ID is included in the payload.
    /// </summary>
    public async Task<StempelResult> StempelAsync(
        StempelRequest request,
        ITimeEntryRepository repository)
    {
        var now = DateTime.UtcNow;
        var effectiveTimestamp = request.TimestampOverrideUtc ?? now;
        var dayStart = effectiveTimestamp.Date;

        var existingEntries = await repository.GetByEmployeeAsync(
            request.EmployeeId, dayStart, dayStart.AddDays(1));

        var validation = _stempelService.ValidateEntry(request.Type, existingEntries);
        if (!validation.IsValid)
            throw new StempelException(validation.Error ?? "Ungültige Stempelung");

        if (_stempelService.IsDuplicateEntry(request.Type, effectiveTimestamp, existingEntries))
            throw new StempelException("Doppelstempelung erkannt (< 5 Sekunden)");

        // Phase 1: insert with placeholder hash to get the real DB Id
        var entry = new TimeEntry
        {
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            TimestampUtc = effectiveTimestamp,
            Source = request.Source,
            CorrectionOfId = request.CorrectionOfId,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAtUtc = now,
            PrevHash = HashChainService.GenesisHash, // temporary
            Hash = HashChainService.GenesisHash      // temporary
        };

        await repository.AddAndSaveAsync(entry); // entry.Id is set after this call

        // Phase 2: now that entry.Id is known, compute correct hash under semaphore
        var prevHash = await repository.GetPrevHashAsync(entry.Id, request.EmployeeId);
        var (finalPrev, finalHash) = await _hashChainService.ComputeAndChainAsync(entry, prevHash);
        await repository.UpdateHashAsync(entry, finalPrev, finalHash);

        var allEntries = existingEntries.Append(entry).ToList();
        var warnings = _arbZGValidator.ValidateDay(now.Date, allEntries);

        return new StempelResult { Entry = entry, Warnings = warnings };
    }

    /// <summary>
    /// Approves a correction request and creates a new TimeEntry with Source=Correction.
    /// </summary>
    public async Task<TimeEntry> ApplyCorrectionAsync(
        CorrectionRequest correction,
        int adminUserId,
        ITimeEntryRepository repository)
    {
        var request = new StempelRequest
        {
            EmployeeId = correction.EmployeeId,
            Type = correction.Type,
            TimestampOverrideUtc = correction.RequestedTimestamp.ToUniversalTime(),
            Source = EntrySource.Correction,
            CorrectionOfId = null,
            CreatedByUserId = adminUserId
        };

        var result = await StempelAsync(request, repository);

        correction.Status = CorrectionStatus.Approved;
        correction.ApprovedByUserId = adminUserId;
        correction.ApprovedAt = DateTime.UtcNow;
        correction.ResultingEntryId = result.Entry.Id;

        return result.Entry;
    }
}

public class StempelRequest
{
    public int EmployeeId { get; set; }
    public TimeEntryType Type { get; set; }
    public DateTime? TimestampOverrideUtc { get; set; }
    public EntrySource Source { get; set; } = EntrySource.Terminal;
    public long? CorrectionOfId { get; set; }
    public int? CreatedByUserId { get; set; }
}

public class StempelResult
{
    public TimeEntry Entry { get; set; } = null!;
    public IList<ValidationWarning> Warnings { get; set; } = new List<ValidationWarning>();
}

public class StempelException : Exception
{
    public StempelException(string message) : base(message) { }
}

public interface ITimeEntryRepository
{
    Task<List<TimeEntry>> GetByEmployeeAsync(int employeeId, DateTime from, DateTime to);
    /// <summary>Gets the hash of the entry immediately before the given entryId for the employee.</summary>
    Task<string> GetPrevHashAsync(long entryId, int employeeId);
    Task AddAndSaveAsync(TimeEntry entry);
    Task UpdateHashAsync(TimeEntry entry, string prevHash, string hash);
    Task AddAsync(TimeEntry entry);
    Task SaveAsync();
}
