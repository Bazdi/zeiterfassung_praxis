using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Core.Models;

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
            optionsBuilder.UseSqlite("Data Source=zeiterfassung.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            entity.HasOne(e => e.Employee).WithMany(e => e.TimeEntries).HasForeignKey(e => e.EmployeeId);
        });

        modelBuilder.Entity<WorkingTimePattern>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EmployeeId, e.ValidFrom });
            entity.HasOne(e => e.Employee).WithMany(e => e.WorkingTimePatterns).HasForeignKey(e => e.EmployeeId);
        });

        modelBuilder.Entity<LeaveEntitlement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EmployeeId, e.Year });
            entity.HasOne(e => e.Employee).WithMany(e => e.LeaveEntitlements).HasForeignKey(e => e.EmployeeId);
        });

        modelBuilder.Entity<CorrectionRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmployeeId);
            entity.HasOne(e => e.Employee).WithMany(e => e.CorrectionRequests).HasForeignKey(e => e.EmployeeId);
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EmployeeId, e.From, e.To });
            entity.HasOne(e => e.Employee).WithMany(e => e.LeaveRequests).HasForeignKey(e => e.EmployeeId);
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
            entity.HasOne(e => e.User).WithMany(e => e.AuditLogs).HasForeignKey(e => e.UserId);
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
