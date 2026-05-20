using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;

namespace Zeiterfassung.Data.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically creates AuditLog entries
/// for every mutation to any tracked entity. Part of GoBD append-only compliance.
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly HashChainService _hashChainService;

    private static readonly HashSet<Type> _auditedTypes = new()
    {
        typeof(Employee),
        typeof(WorkingTimePattern),
        typeof(LeaveEntitlement),
        typeof(CorrectionRequest),
        typeof(LeaveRequest),
        typeof(Holiday),
        typeof(User)
    };

    private int? _currentUserId;

    public AuditInterceptor(HashChainService hashChainService)
    {
        _hashChainService = hashChainService;
    }

    public void SetCurrentUser(int? userId) => _currentUserId = userId;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var db = eventData.Context as ZeiterfassungDbContext;
        if (db == null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        EnforceAppendOnly(db);
        await CreateAuditEntriesAsync(db, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnforceAppendOnly(ZeiterfassungDbContext db)
    {
        // TimeEntry: deletion forbidden. Modification forbidden EXCEPT the
        // one-time hash sealing performed by StempelManager — the two-phase
        // INSERT-then-UPDATE pattern needs the real DB Id in the hash payload,
        // so we allow PrevHash/Hash to change from the GenesisHash placeholder
        // exactly once. Semantic columns (EmployeeId, Type, Timestamp, …)
        // must never be modified.
        var hashSealingProps = new HashSet<string>
        {
            nameof(TimeEntry.PrevHash),
            nameof(TimeEntry.Hash)
        };

        foreach (var entry in db.ChangeTracker.Entries<TimeEntry>())
        {
            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"TimeEntry darf nicht gelöscht werden: #{entry.Entity.Id}");
            }

            if (entry.State != EntityState.Modified) continue;

            var changedProps = entry.Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name)
                .ToHashSet();

            var nonHashChanges = changedProps.Except(hashSealingProps).ToList();
            if (nonHashChanges.Count > 0)
            {
                throw new InvalidOperationException(
                    $"TimeEntry ist append-only. Verbotene Felder geändert auf #{entry.Entity.Id}: " +
                    string.Join(", ", nonHashChanges));
            }

            // Sealing is one-shot: original Hash must still be the placeholder.
            var originalHash = entry.Property(nameof(TimeEntry.Hash)).OriginalValue as string;
            if (originalHash != HashChainService.GenesisHash)
            {
                throw new InvalidOperationException(
                    $"TimeEntry Hash wurde bereits versiegelt — keine erneute Änderung erlaubt: #{entry.Entity.Id}");
            }
        }

        var auditViolations = db.ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.Id)
            .ToList();

        if (auditViolations.Count > 0)
            throw new InvalidOperationException(
                $"AuditLog ist append-only. Verboten: {string.Join(", ", auditViolations)}");
    }

    private async Task CreateAuditEntriesAsync(
        ZeiterfassungDbContext db,
        CancellationToken cancellationToken)
    {
        var entries = db.ChangeTracker.Entries()
            .Where(e => _auditedTypes.Contains(e.Entity.GetType())
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0)
            return;

        var lastAuditEntry = await db.AuditLogs
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var prevHash = lastAuditEntry?.Hash ?? HashChainService.GenesisHash;

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType().Name;
            var entityId = GetEntityId(entry.Entity);
            var action = entry.State.ToString();

            string? oldJson = null;
            string? newJson = null;

            if (entry.State == EntityState.Modified)
            {
                var oldValues = entry.Properties
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                var newValues = entry.Properties
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                oldJson = JsonSerializer.Serialize(oldValues);
                newJson = JsonSerializer.Serialize(newValues);
            }
            else if (entry.State == EntityState.Added)
            {
                var newValues = entry.Properties
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                newJson = JsonSerializer.Serialize(newValues);
            }
            else if (entry.State == EntityState.Deleted)
            {
                var oldValues = entry.Properties
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                oldJson = JsonSerializer.Serialize(oldValues);
            }

            var auditLog = new AuditLog
            {
                UserId = _currentUserId,
                EntityName = entityType,
                EntityId = entityId,
                Action = action,
                OldJson = oldJson,
                NewJson = newJson,
                TimestampUtc = DateTime.UtcNow,
                PrevHash = prevHash,
                Hash = string.Empty
            };

            var (_, hash) = await _hashChainService.ComputeAndChainAuditAsync(auditLog, prevHash);
            auditLog.Hash = hash;
            prevHash = hash;

            db.AuditLogs.Add(auditLog);
        }
    }

    private static string GetEntityId(object entity)
    {
        return entity switch
        {
            Employee e => e.Id.ToString(),
            WorkingTimePattern w => w.Id.ToString(),
            LeaveEntitlement le => le.Id.ToString(),
            CorrectionRequest cr => cr.Id.ToString(),
            LeaveRequest lr => lr.Id.ToString(),
            Holiday h => h.Id.ToString(),
            User u => u.Id.ToString(),
            _ => "unknown"
        };
    }
}
