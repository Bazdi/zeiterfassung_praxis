using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Data.Interceptors;

namespace Zeiterfassung.Data;

public class ZeiterfassungDbContext : DbContext
{
    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<TimeEntry> TimeEntries { get; set; } = null!;
    public DbSet<WorkingTimePattern> WorkingTimePatterns { get; set; } = null!;
    public DbSet<LeaveEntitlement> LeaveEntitlements { get; set; } = null!;
    public DbSet<CorrectionRequest> CorrectionRequests { get; set; } = null!;
    public DbSet<LeaveRequest> LeaveRequests { get; set; } = null!;
    public DbSet<Holiday> Holidays { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;

    public ZeiterfassungDbContext(DbContextOptions<ZeiterfassungDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (!optionsBuilder.IsConfigured)
        {
            // Design-time / migrations fallback only. Runtime configuration
            // (incl. AuditInterceptor) is wired up by AddDbContext in Program.cs.
            optionsBuilder.UseSqlite("Data Source=zeiterfassung.db");
        }
    }

    /// <summary>
    /// Force DateTime.Kind=Utc on every read-back. SQLite has no native
    /// DateTime type and EF Core returns values with Kind=Unspecified by
    /// default. That breaks the hash chain because HashChainService calls
    /// ToUniversalTime() — which on Unspecified treats the value as LOCAL
    /// and shifts it by the local offset, producing a different payload
    /// than at insert time. With this converter every DateTime that
    /// leaves the DB is tagged Utc again, so re-computing the hash from
    /// stored data matches the original.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> _utcConverter =
        new(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> _utcConverterNullable =
        new(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply the UTC converter to every DateTime / DateTime? property
        // in the model. Must run before the explicit entity blocks so the
        // converter is in place when the providers map columns.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(DateTime))      prop.SetValueConverter(_utcConverter);
                else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(_utcConverterNullable);
            }
        }

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PinHash).IsRequired();
            entity.Property(e => e.PinSalt).IsRequired();
        });

        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Hash).IsRequired();
            entity.Property(e => e.PrevHash).IsRequired();
            entity.HasIndex(e => new { e.EmployeeId, e.CreatedAtUtc });
            entity.HasOne(e => e.Employee)
                .WithMany(e => e.TimeEntries)
                .HasForeignKey(e => e.EmployeeId);
        });

        modelBuilder.Entity<WorkingTimePattern>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EmployeeId, e.ValidFrom });
            entity.HasOne(e => e.Employee)
                .WithMany(e => e.WorkingTimePatterns)
                .HasForeignKey(e => e.EmployeeId);
        });

        modelBuilder.Entity<LeaveEntitlement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EmployeeId, e.Year });
            entity.HasOne(e => e.Employee)
                .WithMany(e => e.LeaveEntitlements)
                .HasForeignKey(e => e.EmployeeId);
        });

        modelBuilder.Entity<CorrectionRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmployeeId);
            entity.HasOne(e => e.Employee)
                .WithMany(e => e.CorrectionRequests)
                .HasForeignKey(e => e.EmployeeId);
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EmployeeId, e.From, e.To });
            entity.HasOne(e => e.Employee)
                .WithMany(e => e.LeaveRequests)
                .HasForeignKey(e => e.EmployeeId);
            // DSGVO Art. 9: Krankmeldungen sind Gesundheitsdaten — keine Notiz erlaubt
            // LeaveType.Krank = 1 (Enum-Wert)
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_LeaveRequest_NoNotesForSick",
                "\"Type\" != 1 OR \"Notes\" IS NULL"));
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Date);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Hash).IsRequired();
            entity.Property(e => e.PrevHash).IsRequired();
            entity.HasIndex(e => new { e.EntityName, e.EntityId, e.TimestampUtc });
            entity.HasOne(e => e.User)
                .WithMany(e => e.AuditLogs)
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Roles).IsRequired();
        });
    }
}
