using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

/// <summary>
/// Orchestrates all stamping operations: validates, chains hash, persists.
/// All dependencies are interfaces to allow testing and DB-agnostic design.
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
    /// Creates and persists a new TimeEntry.
    /// Returns warnings if ArbZG rules are at risk.
    /// Throws StempelException if entry is invalid or duplicate.
    /// </summary>
    public async Task<StempelResult> StempelAsync(
        StempelRequest request,
        ITimeEntryRepository repository)
    {
        var now = DateTime.UtcNow;

        // Load existing entries for validation
        var todayStart = now.Date;
        var existingEntries = await repository.GetByEmployeeAsync(
            request.EmployeeId,
            todayStart,
            now.AddDays(1));

        // Validate sequence
        var validation = _stempelService.ValidateEntry(request.Type, existingEntries);
        if (!validation.IsValid)
            throw new StempelException(validation.Error ?? "Ungültige Stempelung");

        // Duplicate check
        if (_stempelService.IsDuplicateEntry(request.Type, now, existingEntries))
            throw new StempelException("Doppelstempelung erkannt (< 5 Sekunden)");

        // Get last hash from DB (must be atomic with INSERT — done inside repository)
        var prevHash = await repository.GetLastHashAsync(request.EmployeeId)
            ?? HashChainService.GenesisHash;

        var entry = new TimeEntry
        {
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            TimestampUtc = request.TimestampOverrideUtc ?? now,
            Source = request.Source,
            CorrectionOfId = request.CorrectionOfId,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAtUtc = now,
            PrevHash = prevHash,
            Hash = string.Empty
        };

        var (chainedPrev, hash) = await _hashChainService.ComputeAndChainAsync(entry, prevHash);
        entry.PrevHash = chainedPrev;
        entry.Hash = hash;

        await repository.AddAsync(entry);

        // Check ArbZG warnings (non-blocking)
        var allEntries = existingEntries.Append(entry).ToList();
        var warnings = _arbZGValidator.ValidateDay(now.Date, allEntries);

        return new StempelResult
        {
            Entry = entry,
            Warnings = warnings
        };
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
    Task<string?> GetLastHashAsync(int employeeId);
    Task AddAsync(TimeEntry entry);
    Task SaveAsync();
}
