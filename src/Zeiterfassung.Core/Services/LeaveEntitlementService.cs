using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class LeaveEntitlementService
{
    public LeaveEntitlement? GetEntitlementForYear(
        int year,
        IList<LeaveEntitlement> entitlements)
    {
        return entitlements
            .FirstOrDefault(e => e.Year == year);
    }

    public decimal GetRemainingDays(
        int year,
        LeaveEntitlement entitlement,
        IList<LeaveRequest> usedLeaves)
    {
        var usedDays = usedLeaves
            .Where(l => l.Type == LeaveType.Urlaub &&
                   l.Status == LeaveRequestStatus.Approved &&
                   l.From.Year == year)
            .Sum(l => (l.To.Date - l.From.Date).Days);

        var totalAvailable = entitlement.EntitlementDays +
                           entitlement.CarriedOverDays +
                           entitlement.SpecialLeaveDays;

        return totalAvailable - usedDays;
    }

    public decimal GetSickDaysUsed(
        int year,
        IList<LeaveRequest> leaves)
    {
        return leaves
            .Where(l => l.Type == LeaveType.Krank &&
                   l.Status == LeaveRequestStatus.Approved &&
                   l.From.Year == year)
            .Sum(l => (l.To.Date - l.From.Date).Days);
    }
}
