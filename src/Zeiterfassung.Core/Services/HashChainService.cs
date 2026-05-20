using System.Security.Cryptography;
using System.Text;
using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class HashChainService
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<string> ComputeHashAsync(string prevHash, string payload)
    {
        var combined = prevHash + "\n" + payload;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public string CreatePayload(TimeEntry entry)
    {
        var parts = new[]
        {
            entry.Id.ToString(),
            entry.EmployeeId.ToString(),
            entry.Type.ToString(),
            entry.TimestampUtc.ToUniversalTime().ToString("O"),
            entry.Source.ToString(),
            entry.CorrectionOfId?.ToString() ?? "",
            entry.CreatedByUserId?.ToString() ?? "",
            entry.CreatedAtUtc.ToUniversalTime().ToString("O")
        };
        return string.Join("|", parts);
    }

    public string CreatePayload(AuditLog entry)
    {
        var parts = new[]
        {
            entry.Id.ToString(),
            entry.UserId?.ToString() ?? "",
            entry.EntityName,
            entry.EntityId,
            entry.Action,
            entry.OldJson ?? "",
            entry.NewJson ?? "",
            entry.TimestampUtc.ToUniversalTime().ToString("O")
        };
        return string.Join("|", parts);
    }

    public async Task<(string PrevHash, string Hash)> GetLastHashAsync(
        IEnumerable<TimeEntry> entries)
    {
        var lastEntry = entries.OrderByDescending(e => e.Id).FirstOrDefault();
        if (lastEntry == null)
        {
            return ("0000000000000000000000000000000000000000000000000000000000000000", "0000000000000000000000000000000000000000000000000000000000000000");
        }
        return (lastEntry.Hash, lastEntry.Hash);
    }

    public async Task<(string PrevHash, string Hash)> GetLastHashAsync(
        IEnumerable<AuditLog> entries)
    {
        var lastEntry = entries.OrderByDescending(e => e.Id).FirstOrDefault();
        if (lastEntry == null)
        {
            return ("0000000000000000000000000000000000000000000000000000000000000000", "0000000000000000000000000000000000000000000000000000000000000000");
        }
        return (lastEntry.Hash, lastEntry.Hash);
    }

    public async Task<HashChainVerificationResult> VerifyChainAsync(
        IList<TimeEntry> entries,
        long? fromId = null,
        long? toId = null)
    {
        var orderedEntries = entries
            .Where(e => !fromId.HasValue || e.Id >= fromId)
            .Where(e => !toId.HasValue || e.Id <= toId)
            .OrderBy(e => e.Id)
            .ToList();

        if (orderedEntries.Count == 0)
        {
            return new HashChainVerificationResult { IsValid = true };
        }

        string prevHash = "0000000000000000000000000000000000000000000000000000000000000000";

        foreach (var entry in orderedEntries)
        {
            if (entry.PrevHash != prevHash)
            {
                return new HashChainVerificationResult
                {
                    IsValid = false,
                    FailedEntryId = entry.Id,
                    ExpectedHash = prevHash,
                    ActualHash = entry.PrevHash
                };
            }

            var payload = CreatePayload(entry);
            var computedHash = await ComputeHashAsync(entry.PrevHash, payload);

            if (computedHash != entry.Hash)
            {
                return new HashChainVerificationResult
                {
                    IsValid = false,
                    FailedEntryId = entry.Id,
                    ExpectedHash = computedHash,
                    ActualHash = entry.Hash
                };
            }

            prevHash = entry.Hash;
        }

        return new HashChainVerificationResult { IsValid = true };
    }

    public async Task<HashChainVerificationResult> VerifyChainAsync(
        IList<AuditLog> entries,
        long? fromId = null,
        long? toId = null)
    {
        var orderedEntries = entries
            .Where(e => !fromId.HasValue || e.Id >= fromId)
            .Where(e => !toId.HasValue || e.Id <= toId)
            .OrderBy(e => e.Id)
            .ToList();

        if (orderedEntries.Count == 0)
        {
            return new HashChainVerificationResult { IsValid = true };
        }

        string prevHash = "0000000000000000000000000000000000000000000000000000000000000000";

        foreach (var entry in orderedEntries)
        {
            if (entry.PrevHash != prevHash)
            {
                return new HashChainVerificationResult
                {
                    IsValid = false,
                    FailedEntryId = entry.Id,
                    ExpectedHash = prevHash,
                    ActualHash = entry.PrevHash
                };
            }

            var payload = CreatePayload(entry);
            var computedHash = await ComputeHashAsync(entry.PrevHash, payload);

            if (computedHash != entry.Hash)
            {
                return new HashChainVerificationResult
                {
                    IsValid = false,
                    FailedEntryId = entry.Id,
                    ExpectedHash = computedHash,
                    ActualHash = entry.Hash
                };
            }

            prevHash = entry.Hash;
        }

        return new HashChainVerificationResult { IsValid = true };
    }
}

public class HashChainVerificationResult
{
    public bool IsValid { get; set; }
    public long? FailedEntryId { get; set; }
    public string? ExpectedHash { get; set; }
    public string? ActualHash { get; set; }
}
