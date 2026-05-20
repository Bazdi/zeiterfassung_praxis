using Zeiterfassung.Core.Models;

namespace Zeiterfassung.Core.Services;

public class PinAuthenticationService
{
    private readonly PinService _pinService;

    private const int MaxAttemptsBefore60sLock = 5;
    private const int MaxAttemptsBefore5MinLock = 6;

    public PinAuthenticationService(PinService pinService)
    {
        _pinService = pinService;
    }

    /// <summary>
    /// Validates a PIN with brute-force protection:
    /// 5 failed attempts → 60s lockout
    /// 6th failed attempt → 5min lockout
    /// 7th+ → requires Admin reset
    /// </summary>
    public PinAuthResult Authenticate(Employee employee, string enteredPin)
    {
        if (employee.LockedUntil.HasValue && employee.LockedUntil > DateTime.UtcNow)
        {
            var remainingSeconds = (int)(employee.LockedUntil.Value - DateTime.UtcNow).TotalSeconds;
            return new PinAuthResult
            {
                Success = false,
                RequiresAdminReset = employee.FailedPinAttempts >= MaxAttemptsBefore5MinLock + 1,
                LockoutRemainingSeconds = remainingSeconds
            };
        }

        var isValid = _pinService.VerifyPin(enteredPin, employee.PinHash, employee.PinSalt);

        if (!isValid)
        {
            employee.FailedPinAttempts++;

            if (employee.FailedPinAttempts >= MaxAttemptsBefore5MinLock)
            {
                // 6th attempt: 5 min lockout, further attempts need Admin reset
                employee.LockedUntil = DateTime.UtcNow.AddMinutes(5);
            }
            else if (employee.FailedPinAttempts >= MaxAttemptsBefore60sLock)
            {
                // 5th attempt: 60s lockout
                employee.LockedUntil = DateTime.UtcNow.AddSeconds(60);
            }

            return new PinAuthResult
            {
                Success = false,
                RequiresAdminReset = employee.FailedPinAttempts > MaxAttemptsBefore5MinLock,
                RemainingAttempts = Math.Max(0, MaxAttemptsBefore60sLock - employee.FailedPinAttempts)
            };
        }

        // Success: reset lockout state
        employee.FailedPinAttempts = 0;
        employee.LockedUntil = null;

        return new PinAuthResult { Success = true };
    }

    /// <summary>
    /// Resets PIN and lockout state. Admin generates a random new PIN
    /// which the employee must change at next login.
    /// </summary>
    public string ResetPin(Employee employee)
    {
        var newPin = _pinService.GenerateRandomPin();
        var (hash, salt) = _pinService.HashPin(newPin);
        employee.PinHash = hash;
        employee.PinSalt = salt;
        employee.PinChangedAt = null; // Forces PIN change at next login
        employee.FailedPinAttempts = 0;
        employee.LockedUntil = null;
        return newPin;
    }

    public bool NeedsPinChange(Employee employee)
    {
        return employee.PinChangedAt == null;
    }
}

public class PinAuthResult
{
    public bool Success { get; set; }
    public bool RequiresAdminReset { get; set; }
    public int? RemainingAttempts { get; set; }
    public int? LockoutRemainingSeconds { get; set; }
}
