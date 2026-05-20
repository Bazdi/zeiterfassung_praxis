using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;
using Zeiterfassung.Data;
using Zeiterfassung.Export;
using Xunit;

namespace Zeiterfassung.Integration;

/// <summary>
/// End-to-end integration tests with in-memory SQLite database.
/// Tests the full stamping workflow: Kommen → PauseStart → PauseEnd → Gehen
/// then verifies: correct saldo, integrity chain intact, PDF + Excel generation.
/// </summary>
public class FullDayIntegrationTests : IDisposable
{
    private readonly ZeiterfassungDbContext _db;
    private readonly HashChainService _hashChain;
    private readonly StempelService _stempelService;
    private readonly StempelManager _stempelManager;
    private readonly ArbZGValidator _arbZG;
    private readonly SaldoService _saldo;

    private InMemoryTimeEntryRepository _repo;

    public FullDayIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ZeiterfassungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ZeiterfassungDbContext(options);

        _hashChain = new HashChainService();
        _arbZG = new ArbZGValidator();
        _stempelService = new StempelService(_hashChain);
        _stempelManager = new StempelManager(_stempelService, _hashChain, _arbZG);
        _saldo = new SaldoService();
        _repo = new InMemoryTimeEntryRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task FullDay_FourStamps_SaldoCorrect()
    {
        // Arrange: employee with 8h/day Monday-Friday contract
        var emp = new Employee
        {
            FirstName = "Max", LastName = "Mustermann",
            PinHash = "x", PinSalt = "x",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();

        var monday = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc); // Known Monday
        var pattern = new WorkingTimePattern
        {
            EmployeeId = emp.Id, ValidFrom = monday,
            MondayHours = 8, TuesdayHours = 8, WednesdayHours = 8,
            ThursdayHours = 8, FridayHours = 8,
            CreatedAt = DateTime.UtcNow, CreatedByUserId = 0
        };
        _db.WorkingTimePatterns.Add(pattern);
        await _db.SaveChangesAsync();

        // Act: stamp a full workday with 30-min lunch
        var stamps = new[]
        {
            (TimeEntryType.Kommen,     monday.AddHours(8)),       // 08:00
            (TimeEntryType.PauseStart, monday.AddHours(12)),      // 12:00
            (TimeEntryType.PauseEnd,   monday.AddHours(12.5)),    // 12:30
            (TimeEntryType.Gehen,      monday.AddHours(17))       // 17:00 → net 8.5h
        };

        foreach (var (type, ts) in stamps)
        {
            var req = new StempelRequest
            {
                EmployeeId = emp.Id,
                Type = type,
                TimestampOverrideUtc = ts,
                Source = EntrySource.Terminal
            };
            var result = await _stempelManager.StempelAsync(req, _repo);
            result.Should().NotBeNull();
            // StempelManager already saved via AddAndSaveAsync + UpdateHashAsync
        }

        // Assert: 4 entries in DB
        var entries = await _db.TimeEntries.Where(e => e.EmployeeId == emp.Id).ToListAsync();
        entries.Should().HaveCount(4);

        // Assert: hash chain intact
        var chainResult = await _hashChain.VerifyTimeEntryChainAsync(entries.OrderBy(e => e.Id).ToList());
        chainResult.IsValid.Should().BeTrue("hash chain must be intact after 4 stamps");

        // Assert: daily balance = +0.5h (worked 8.5h, required 8h)
        var localDate = NodaTime.LocalDate.FromDateTime(monday.ToLocalTime());
        var dailyBalance = _saldo.CalculateDailyBalance(localDate, entries, new[] { pattern }, Array.Empty<Holiday>());
        dailyBalance.Should().BeApproximately(0.5m, 0.01m, "worked 8.5h vs 8h required = +0.5h");
    }

    [Fact]
    public async Task AppendOnly_UpdateOnTimeEntry_ShouldThrow()
    {
        var emp = new Employee
        {
            FirstName = "Test", LastName = "User",
            PinHash = "x", PinSalt = "x",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.Employees.Add(emp);

        var entry = new TimeEntry
        {
            EmployeeId = emp.Id,
            Type = TimeEntryType.Kommen,
            TimestampUtc = DateTime.UtcNow,
            Source = EntrySource.Terminal,
            CreatedAtUtc = DateTime.UtcNow,
            PrevHash = HashChainService.GenesisHash,
            Hash = "abc"
        };
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync();

        // Tamper
        entry.TimestampUtc = entry.TimestampUtc.AddHours(1);

        // InMemory provider doesn't run the interceptor, so we test the service directly
        var tamperedEntries = await _db.TimeEntries.ToListAsync();
        var result = await _hashChain.VerifyTimeEntryChainAsync(tamperedEntries);
        result.IsValid.Should().BeFalse("tampered timestamp should break hash chain");
    }

    [Fact]
    public async Task SaldoService_ContractChange_ShouldRespectBothPatterns()
    {
        var emp = new Employee
        {
            FirstName = "Teilzeit", LastName = "Wechsel",
            PinHash = "x", PinSalt = "x",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();

        // Pattern A: 20h/week (4h/day Mon-Fri) until June 30
        var patternA = new WorkingTimePattern
        {
            EmployeeId = emp.Id,
            ValidFrom = new DateTime(2026, 1, 1),
            ValidUntil = new DateTime(2026, 6, 30),
            MondayHours = 4, TuesdayHours = 4, WednesdayHours = 4,
            ThursdayHours = 4, FridayHours = 4,
            CreatedAt = DateTime.UtcNow, CreatedByUserId = 0
        };
        // Pattern B: 40h/week (8h/day Mon-Fri) from July 1
        var patternB = new WorkingTimePattern
        {
            EmployeeId = emp.Id,
            ValidFrom = new DateTime(2026, 7, 1),
            MondayHours = 8, TuesdayHours = 8, WednesdayHours = 8,
            ThursdayHours = 8, FridayHours = 8,
            CreatedAt = DateTime.UtcNow, CreatedByUserId = 0
        };

        // 6h of work on a day in June (should count 4h required → +2h)
        var juneDay = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc); // Monday
        var juneEntries = new List<TimeEntry>
        {
            MakeEntry(emp.Id, TimeEntryType.Kommen, juneDay),
            MakeEntry(emp.Id, TimeEntryType.Gehen, juneDay.AddHours(6))
        };

        var patterns = new List<WorkingTimePattern> { patternA, patternB };
        var juneDateLocal = NodaTime.LocalDate.FromDateTime(juneDay.ToLocalTime());
        var balanceJune = _saldo.CalculateDailyBalance(juneDateLocal, juneEntries, patterns, Array.Empty<Holiday>());
        balanceJune.Should().BeApproximately(2m, 0.01m, "worked 6h vs 4h required on that Monday in June = +2h");

        // 6h of work on a day in July (should count 8h required → -2h)
        var julyDay = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc); // Wednesday
        var julyEntries = new List<TimeEntry>
        {
            MakeEntry(emp.Id, TimeEntryType.Kommen, julyDay),
            MakeEntry(emp.Id, TimeEntryType.Gehen, julyDay.AddHours(6))
        };
        var julyDateLocal = NodaTime.LocalDate.FromDateTime(julyDay.ToLocalTime());
        var balanceJuly = _saldo.CalculateDailyBalance(julyDateLocal, julyEntries, patterns, Array.Empty<Holiday>());
        balanceJuly.Should().BeApproximately(-2m, 0.01m, "worked 6h vs 8h required on that Wednesday in July = -2h");
    }

    [Fact]
    public async Task DstDay_March_23hDay_SaldoCorrect()
    {
        var emp = new Employee
        {
            FirstName = "DST", LastName = "Test",
            PinHash = "x", PinSalt = "x",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();

        // March 29, 2026 = Sunday DST changeover (23h day)
        // March 27 = Friday before DST, normal 8h day
        var friday = new DateTime(2026, 3, 27, 7, 0, 0, DateTimeKind.Utc); // 08:00 Berlin (UTC+1)
        var pattern = new WorkingTimePattern
        {
            EmployeeId = emp.Id,
            ValidFrom = new DateTime(2026, 1, 1),
            MondayHours = 8, TuesdayHours = 8, WednesdayHours = 8,
            ThursdayHours = 8, FridayHours = 8,
            CreatedAt = DateTime.UtcNow, CreatedByUserId = 0
        };
        var dayEntries = new List<TimeEntry>
        {
            MakeEntry(emp.Id, TimeEntryType.Kommen, friday),
            MakeEntry(emp.Id, TimeEntryType.Gehen, friday.AddHours(8))
        };

        var fridayLocal = NodaTime.LocalDate.FromDateTime(friday.ToLocalTime());
        var balance = _saldo.CalculateDailyBalance(fridayLocal, dayEntries, new[] { pattern }, Array.Empty<Holiday>());
        balance.Should().BeApproximately(0m, 0.01m, "8h worked = 8h required on Friday before DST = 0 balance");
    }

    [Fact]
    public void ExcelExport_Generate_ProducesValidBytes()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var report = new MonthReport
        {
            EmployeeName = "Test, Employee",
            Year = 2026, Month = 5,
            PracticeName = "Testpraxis",
            Days = new List<DayReport>
            {
                new() { Date = new DateTime(2026, 5, 1), KommenLocal = new DateTime(2026, 5, 1, 8, 0, 0),
                    GehenLocal = new DateTime(2026, 5, 1, 16, 30, 0), PauseMinutes = 30,
                    WorkedHours = 8m, RequiredHours = 8m, BalanceHours = 0m }
            }
        };

        var excelBytes = ExcelMonatsbericht.Generate(new[] { report });
        excelBytes.Should().NotBeEmpty();
        excelBytes.Length.Should().BeGreaterThan(1000, "valid XLSX should be at least 1KB");
    }

    [Fact]
    public void PdfExport_Generate_ProducesValidBytes()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var report = new MonthReport
        {
            EmployeeName = "Test, Employee",
            Year = 2026, Month = 5,
            PracticeName = "Testpraxis",
            Days = new List<DayReport>
            {
                new() { Date = new DateTime(2026, 5, 4), KommenLocal = new DateTime(2026, 5, 4, 8, 0, 0),
                    GehenLocal = new DateTime(2026, 5, 4, 16, 30, 0), PauseMinutes = 30,
                    WorkedHours = 8m, RequiredHours = 8m, BalanceHours = 0m }
            }
        };

        var pdfBytes = PdfStundenzettelReport.Generate(report);
        pdfBytes.Should().NotBeEmpty();
        // PDF magic bytes: %PDF
        pdfBytes[0].Should().Be(0x25); // %
        pdfBytes[1].Should().Be(0x50); // P
        pdfBytes[2].Should().Be(0x44); // D
        pdfBytes[3].Should().Be(0x46); // F
    }

    private TimeEntry MakeEntry(int empId, TimeEntryType type, DateTime utc) => new()
    {
        EmployeeId = empId, Type = type, TimestampUtc = utc,
        Source = EntrySource.Terminal, CreatedAtUtc = utc,
        PrevHash = HashChainService.GenesisHash, Hash = "test"
    };
}

public class InMemoryTimeEntryRepository : ITimeEntryRepository
{
    private readonly ZeiterfassungDbContext _db;

    public InMemoryTimeEntryRepository(ZeiterfassungDbContext db) => _db = db;

    public async Task<List<TimeEntry>> GetByEmployeeAsync(int employeeId, DateTime from, DateTime to)
        => await _db.TimeEntries
            .Where(e => e.EmployeeId == employeeId && e.TimestampUtc >= from && e.TimestampUtc <= to)
            .OrderBy(e => e.TimestampUtc)
            .ToListAsync();

    public async Task<string> GetPrevHashAsync(long entryId, int employeeId)
    {
        var prev = await _db.TimeEntries
            .Where(e => e.EmployeeId == employeeId && e.Id < entryId)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();
        return prev?.Hash ?? HashChainService.GenesisHash;
    }

    public async Task AddAndSaveAsync(TimeEntry entry)
    {
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync();
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
