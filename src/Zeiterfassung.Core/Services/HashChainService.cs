using System.Security.Cryptography;
using System.Text;
using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class HashChainService
{
    private static readonly SemaphoreSlim _timeEntrySemaphore = new(1, 1);
    private static readonly SemaphoreSlim _auditLogSemaphore = new(1, 1);

    public static readonly string GenesisHash =
        new string('0', 64);

    public string ComputeHash(string prevHash, string payload)
    {
        var combined = prevHash + "\n" + payload;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public string CreatePayload(TimeEntry entry) =>
        string.Join("|", new[]
        {
            entry.Id.ToString(),
            entry.EmployeeId.ToString(),
            entry.Type.ToString(),
            entry.TimestampUtc.ToUniversalTime().ToString("O"),
            entry.Source.ToString(),
            entry.CorrectionOfId?.ToString() ?? "",
            entry.CreatedByUserId?.ToString() ?? "",
            entry.CreatedAtUtc.ToUniversalTime().ToString("O")
        });

    /// <summary>
    /// AuditLog payload. NOTE: Id is intentionally NOT included.
    /// AuditInterceptor computes the hash inside SavingChangesAsync —
    /// before EF assigns the auto-increment Id — so including Id in the
    /// payload would mean hashing against Id=0 and verifying against the
    /// stored Id (e.g. 7), producing a guaranteed false-positive
    /// "manipulation" finding. The chain's tamper-evidence is provided
    /// by the PrevHash links plus the hashed content; row position is
    /// implicitly checked because reordering would break PrevHash.
    /// </summary>
    public string CreatePayload(AuditLog entry) =>
        string.Join("|", new[]
        {
            entry.UserId?.ToString() ?? "",
            entry.EntityName,
            entry.EntityId,
            entry.Action,
            entry.OldJson ?? "",
            entry.NewJson ?? "",
            entry.TimestampUtc.ToUniversalTime().ToString("O")
        });

    /// <summary>
    /// Appends a TimeEntry to the hash chain under a semaphore.
    /// Caller must pass in the current last hash from the DB (within a transaction).
    /// </summary>
    public async Task<(string PrevHash, string Hash)> ComputeAndChainAsync(
        TimeEntry entry,
        string prevHash)
    {
        await _timeEntrySemaphore.WaitAsync();
        try
        {
            var payload = CreatePayload(entry);
            var hash = ComputeHash(prevHash, payload);
            return (prevHash, hash);
        }
        finally
        {
            _timeEntrySemaphore.Release();
        }
    }

    /// <summary>
    /// Appends an AuditLog entry to the audit hash chain under a semaphore.
    /// </summary>
    public async Task<(string PrevHash, string Hash)> ComputeAndChainAuditAsync(
        AuditLog entry,
        string prevHash)
    {
        await _auditLogSemaphore.WaitAsync();
        try
        {
            var payload = CreatePayload(entry);
            var hash = ComputeHash(prevHash, payload);
            return (prevHash, hash);
        }
        finally
        {
            _auditLogSemaphore.Release();
        }
    }

    public async Task<HashChainVerificationResult> VerifyTimeEntryChainAsync(
        IList<TimeEntry> entries,
        long? fromId = null,
        long? toId = null)
    {
        var orderedEntries = entries
            .Where(e => !fromId.HasValue || e.Id >= fromId)
            .Where(e => !toId.HasValue || e.Id <= toId)
            .OrderBy(e => e.Id)
            .ToList();

        return await VerifyChainInternalAsync(
            orderedEntries,
            e => e.Id,
            e => e.PrevHash,
            e => e.Hash,
            e => CreatePayload(e));
    }

    public async Task<HashChainVerificationResult> VerifyAuditLogChainAsync(
        IList<AuditLog> entries,
        long? fromId = null,
        long? toId = null)
    {
        var orderedEntries = entries
            .Where(e => !fromId.HasValue || e.Id >= fromId)
            .Where(e => !toId.HasValue || e.Id <= toId)
            .OrderBy(e => e.Id)
            .ToList();

        return await VerifyChainInternalAsync(
            orderedEntries,
            e => e.Id,
            e => e.PrevHash,
            e => e.Hash,
            e => CreatePayload(e));
    }

    private Task<HashChainVerificationResult> VerifyChainInternalAsync<T>(
        IList<T> entries,
        Func<T, long> getId,
        Func<T, string> getPrevHash,
        Func<T, string> getHash,
        Func<T, string> createPayload)
    {
        if (entries.Count == 0)
            return Task.FromResult(new HashChainVerificationResult { IsValid = true });

        var prevHash = GenesisHash;
        foreach (var entry in entries)
        {
            if (getPrevHash(entry) != prevHash)
            {
                return Task.FromResult(new HashChainVerificationResult
                {
                    IsValid = false,
                    FailedEntryId = getId(entry),
                    ExpectedPrevHash = prevHash,
                    ActualPrevHash = getPrevHash(entry)
                });
            }

            var computedHash = ComputeHash(getPrevHash(entry), createPayload(entry));
            if (computedHash != getHash(entry))
            {
                return Task.FromResult(new HashChainVerificationResult
                {
                    IsValid = false,
                    FailedEntryId = getId(entry),
                    ExpectedHash = computedHash,
                    ActualHash = getHash(entry)
                });
            }

            prevHash = getHash(entry);
        }

        return Task.FromResult(new HashChainVerificationResult { IsValid = true });
    }
}

public class HashChainVerificationResult
{
    public bool IsValid { get; set; }
    public long? FailedEntryId { get; set; }
    public string? ExpectedPrevHash { get; set; }
    public string? ActualPrevHash { get; set; }
    public string? ExpectedHash { get; set; }
    public string? ActualHash { get; set; }
}
