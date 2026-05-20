using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;
using Zeiterfassung.Data;

namespace Zeiterfassung.Web.Services;

/// <summary>
/// Populates the demo database with a realistic snapshot of a small medical
/// practice: 1 admin, 6 active employees with known PINs, working-time
/// patterns, ~14 days of historical stamps (with realistic Kommen/Pause/Gehen
/// patterns), a handful of pending correction requests, and the typical
/// German bank holidays for the current year.
///
/// Determinism: every run produces the same data so screenshots, demos
/// and tests are stable. The starting date is "today minus 14" so the
/// demo always looks fresh.
/// </summary>
public class DemoDataSeeder
{
    public sealed record DemoEmployee(string First, string Last, string Pin, double[] Pattern, int FailedAttempts);

    /// <summary>The 6 demo employees with their known PINs.</summary>
    public static readonly IReadOnlyList<DemoEmployee> Employees = new[]
    {
        new DemoEmployee("Maja",     "Winter",     "100001", new[] { 8d, 8, 8, 8, 8, 0, 0 }, 0),
        new DemoEmployee("Tobias",   "Köhler",     "100002", new[] { 8d, 8, 8, 8, 8, 0, 0 }, 0),
        new DemoEmployee("Sara",     "Nowak",      "100003", new[] { 6d, 6, 8, 8, 8, 0, 0 }, 1),
        new DemoEmployee("Jonas",    "Brückner",   "100004", new[] { 8d, 8, 8, 8, 6, 0, 0 }, 3),
        new DemoEmployee("Leyla",    "Hadji",      "100005", new[] { 6d, 6, 6, 6, 6, 0, 0 }, 0),
        new DemoEmployee("Florian",  "Rademacher", "100006", new[] { 8d, 8, 8, 8, 8, 0, 0 }, 0),
    };

    private readonly ZeiterfassungDbContext _db;
    private readonly PinService _pinService;
    private readonly AdminAuthService _adminAuth;
    private readonly StempelManager _stempelManager;
    private readonly Zeiterfassung.Core.Services.ITimeEntryRepository _timeRepo;

    public DemoDataSeeder(
        ZeiterfassungDbContext db,
        PinService pinService,
        AdminAuthService adminAuth,
        StempelManager stempelManager,
        Zeiterfassung.Core.Services.ITimeEntryRepository timeRepo)
    {
        _db = db;
        _pinService = pinService;
        _adminAuth = adminAuth;
        _stempelManager = stempelManager;
        _timeRepo = timeRepo;
    }

    public async Task SeedAsync(DemoModeOptions opts, CancellationToken ct = default)
    {
        // 1) Admin user
        var adminEmployee = new Employee
        {
            FirstName = "Dr. Beate",
            LastName = "Schmitt",
            IsAdmin = true,
            IsActive = true,
            PinHash = "setup",
            PinSalt = "setup",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Employees.Add(adminEmployee);
        await _db.SaveChangesAsync(ct);

        var admin = _adminAuth.CreateAdminUser(opts.AdminUsername, opts.AdminPassword, adminEmployee.Id);
        _db.Users.Add(admin);
        await _db.SaveChangesAsync(ct);

        // 2) Six employees with known PINs
        var seedEmps = new List<Employee>(Employees.Count);
        foreach (var demo in Employees)
        {
            var (hash, salt) = _pinService.HashPin(demo.Pin);
            var e = new Employee
            {
                FirstName = demo.First,
                LastName = demo.Last,
                Email = $"{demo.First[..1].ToLowerInvariant()}.{demo.Last.ToLowerInvariant().Replace("ä","ae").Replace("ö","oe").Replace("ü","ue")}@demo.praxis",
                PinHash = hash,
                PinSalt = salt,
                PinChangedAt = DateTime.UtcNow.AddDays(-30),
                IsActive = true,
                IsAdmin = false,
                FailedPinAttempts = demo.FailedAttempts,
                CreatedAt = DateTime.UtcNow.AddYears(-2),
            };
            _db.Employees.Add(e);
            seedEmps.Add(e);
        }
        await _db.SaveChangesAsync(ct);

        // 3) Working-time pattern per employee, valid from 1 year ago
        var patternStart = DateTime.UtcNow.AddYears(-1).Date;
        for (int i = 0; i < seedEmps.Count; i++)
        {
            var p = Employees[i].Pattern;
            _db.WorkingTimePatterns.Add(new WorkingTimePattern
            {
                EmployeeId = seedEmps[i].Id,
                ValidFrom = patternStart,
                ValidUntil = null,
                MondayHours    = (decimal)p[0],
                TuesdayHours   = (decimal)p[1],
                WednesdayHours = (decimal)p[2],
                ThursdayHours  = (decimal)p[3],
                FridayHours    = (decimal)p[4],
                SaturdayHours  = (decimal)p[5],
                SundayHours    = (decimal)p[6],
                CreatedAt = DateTime.UtcNow.AddYears(-1),
                CreatedByUserId = admin.Id,
            });
        }
        await _db.SaveChangesAsync(ct);

        // 4) Holidays — the typical fixed German bank holidays for the year
        await SeedHolidaysAsync(ct);

        // 5) Historical stamps — 14 calendar days back, weekdays only, going
        //    through the real StempelManager so the hash chain is valid.
        var rand = new Random(4711); // deterministic
        var today = DateTime.UtcNow.Date;

        for (int dayOffset = 14; dayOffset >= 1; dayOffset--)
        {
            var date = today.AddDays(-dayOffset);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            foreach (var emp in seedEmps)
            {
                // Random 10% sick / vacation day → skip stamping
                if (rand.NextDouble() < 0.05) continue;

                var pattern = Employees[seedEmps.IndexOf(emp)].Pattern;
                var dayHours = pattern[((int)date.DayOfWeek + 6) % 7]; // Mon=0, …, Sun=6
                if (dayHours <= 0) continue;

                await StampDayAsync(emp.Id, date, dayHours, rand, ct);
            }
        }

        // 6) "Today" — partial day, some are clocked in, one on break
        foreach (var (emp, idx) in seedEmps.Select((e, i) => (e, i)))
        {
            var pattern = Employees[idx].Pattern;
            var dayHours = pattern[((int)today.DayOfWeek + 6) % 7];
            if (today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (dayHours <= 0) continue;

            // 3 anwesend, 1 in pause, 1 gegangen, 1 noch nicht da
            var who = idx % 6;
            var kommen = today.AddHours(7).AddMinutes(45 + rand.Next(0, 20));

            if (who == 0)
            {
                await StampAtAsync(emp.Id, TimeEntryType.Kommen, kommen, ct);
            }
            else if (who == 1)
            {
                await StampAtAsync(emp.Id, TimeEntryType.Kommen, kommen, ct);
            }
            else if (who == 2)
            {
                // In pause
                await StampAtAsync(emp.Id, TimeEntryType.Kommen, kommen, ct);
                await StampAtAsync(emp.Id, TimeEntryType.PauseStart, today.AddHours(11).AddMinutes(30), ct);
            }
            else if (who == 3)
            {
                // gegangen (full day) — only if it's late enough for it to make sense
                if (DateTime.UtcNow.Hour >= 14)
                {
                    await StampAtAsync(emp.Id, TimeEntryType.Kommen, kommen, ct);
                    await StampAtAsync(emp.Id, TimeEntryType.PauseStart, today.AddHours(12), ct);
                    await StampAtAsync(emp.Id, TimeEntryType.PauseEnd, today.AddHours(12.5), ct);
                    await StampAtAsync(emp.Id, TimeEntryType.Gehen, today.AddHours(7.75 + dayHours + 0.5), ct);
                }
                else
                {
                    await StampAtAsync(emp.Id, TimeEntryType.Kommen, kommen, ct);
                }
            }
            else if (who == 4)
            {
                await StampAtAsync(emp.Id, TimeEntryType.Kommen, kommen, ct);
            }
            // who == 5 → noch nicht gestempelt heute
        }

        // 7) A few correction requests in mixed states
        var sara = seedEmps.First(e => e.LastName == "Nowak");
        var tobias = seedEmps.First(e => e.LastName == "Köhler");
        var maja = seedEmps.First(e => e.LastName == "Winter");

        _db.CorrectionRequests.AddRange(
            new CorrectionRequest
            {
                EmployeeId = sara.Id,
                Type = TimeEntryType.PauseEnd,
                RequestedTimestamp = today.AddDays(-2).AddHours(13).AddMinutes(15),
                Reason = "Beim Eintreffen war das Terminal blockiert. Stempelung erfolgte erst 15 Min. später durch Frau Köhler.",
                Status = CorrectionStatus.Open,
                CreatedAt = DateTime.UtcNow.AddMinutes(-14),
            },
            new CorrectionRequest
            {
                EmployeeId = tobias.Id,
                Type = TimeEntryType.Gehen,
                RequestedTimestamp = today.AddDays(-3).AddHours(17).AddMinutes(48),
                Reason = "Bin schon abgestempelt vom Schreibtisch gegangen, habe es vergessen über das Terminal zu bestätigen.",
                Status = CorrectionStatus.Open,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
            },
            new CorrectionRequest
            {
                EmployeeId = maja.Id,
                Type = TimeEntryType.Kommen,
                RequestedTimestamp = today.AddDays(-4).AddHours(7).AddMinutes(42),
                Reason = "Verspätung durch S-Bahn-Ausfall — meine Kollegin Frau Köhler kann das bestätigen.",
                Status = CorrectionStatus.Open,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
            },
            new CorrectionRequest
            {
                EmployeeId = sara.Id,
                Type = TimeEntryType.Kommen,
                RequestedTimestamp = today.AddDays(-7).AddHours(7).AddMinutes(48),
                Reason = "Stempelkarte fehlerhaft",
                Status = CorrectionStatus.Approved,
                ApprovedByUserId = admin.Id,
                ApprovedAt = DateTime.UtcNow.AddDays(-6),
                CreatedAt = DateTime.UtcNow.AddDays(-7),
            });
        await _db.SaveChangesAsync(ct);

        // 8) Leave entitlements for current year
        var year = today.Year;
        foreach (var e in seedEmps)
        {
            _db.LeaveEntitlements.Add(new LeaveEntitlement
            {
                EmployeeId = e.Id,
                Year = year,
                EntitlementDays = 30,
                CarriedOverDays = 0,
                SpecialLeaveDays = 0,
                CreatedAt = DateTime.UtcNow.AddMonths(-(today.Month - 1)),
                CreatedByUserId = admin.Id,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedHolidaysAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var fixedHolidays = new (DateTime Date, string Name)[]
        {
            (new DateTime(year, 1, 1),  "Neujahr"),
            (new DateTime(year, 5, 1),  "Tag der Arbeit"),
            (new DateTime(year, 10, 3), "Tag der Deutschen Einheit"),
            (new DateTime(year, 12, 25),"1. Weihnachtstag"),
            (new DateTime(year, 12, 26),"2. Weihnachtstag"),
        };
        foreach (var (d, n) in fixedHolidays)
        {
            _db.Holidays.Add(new Holiday { Date = d, Name = n, State = "HH" });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task StampDayAsync(int employeeId, DateTime date, double targetHours, Random rand, CancellationToken ct)
    {
        // Realistic noise: arrive 07:45–08:10, pause 30–45 min around 12:00, leave ≈ arrival + targetHours + pause
        var kommen     = date.AddHours(7).AddMinutes(45 + rand.Next(0, 25));
        var pauseStart = date.AddHours(12).AddMinutes(rand.Next(-15, 15));
        var pauseMin   = 30 + rand.Next(0, 15);
        var pauseEnd   = pauseStart.AddMinutes(pauseMin);
        var gehen      = kommen.AddHours(targetHours).AddMinutes(pauseMin + rand.Next(-10, 20));

        await StampAtAsync(employeeId, TimeEntryType.Kommen,    kommen,     ct);
        await StampAtAsync(employeeId, TimeEntryType.PauseStart, pauseStart, ct);
        await StampAtAsync(employeeId, TimeEntryType.PauseEnd,   pauseEnd,   ct);
        await StampAtAsync(employeeId, TimeEntryType.Gehen,      gehen,      ct);
    }

    private async Task StampAtAsync(int employeeId, TimeEntryType type, DateTime timestamp, CancellationToken ct)
    {
        try
        {
            await _stempelManager.StempelAsync(
                new StempelRequest
                {
                    EmployeeId = employeeId,
                    Type = type,
                    TimestampOverrideUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
                    Source = EntrySource.Migration, // mark seed data clearly
                },
                _timeRepo);
        }
        catch (StempelException)
        {
            // Skip days where the deterministic seed accidentally creates a
            // sequencing conflict — the seed is best-effort, not a strict
            // simulation.
        }
    }
}
