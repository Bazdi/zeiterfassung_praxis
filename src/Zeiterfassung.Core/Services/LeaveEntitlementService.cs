using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class LeaveEntitlementService
{
    public LeaveEntitlement? GetEntitlementForYear(
        int year,
        IList<LeaveEntitlement> entitlements)
    {
        return entitlements.FirstOrDefault(e => e.Year == year);
    }

    /// <summary>
    /// Calculates remaining vacation days. Uses Days+1 for inclusive day counting.
    /// e.g. Leave from Jan 1 to Jan 3 = 3 days taken.
    /// </summary>
    public decimal GetRemainingDays(
        int year,
        LeaveEntitlement entitlement,
        IList<LeaveRequest> leaves)
    {
        var usedDays = leaves
            .Where(l => l.Type == LeaveType.Urlaub
                && l.Status == LeaveRequestStatus.Approved
                && l.From.Year == year)
            .Sum(l => (l.To.Date - l.From.Date).Days + 1);

        var totalAvailable = entitlement.EntitlementDays
            + entitlement.CarriedOverDays
            + entitlement.SpecialLeaveDays;

        return totalAvailable - usedDays;
    }

    /// <summary>
    /// Counts sick days used in a year. Inclusive counting same as vacation.
    /// </summary>
    public decimal GetSickDaysUsed(int year, IList<LeaveRequest> leaves)
    {
        return leaves
            .Where(l => l.Type == LeaveType.Krank
                && l.Status == LeaveRequestStatus.Approved
                && l.From.Year == year)
            .Sum(l => (l.To.Date - l.From.Date).Days + 1);
    }
}
