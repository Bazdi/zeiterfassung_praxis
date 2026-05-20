using FluentAssertions;
using Zeiterfassung.Core.Models;
using Zeiterfassung.Core.Services;
using Xunit;

namespace Zeiterfassung.Core.Tests;

public class PinAuthenticationServiceTests
{
    private static (Employee emp, PinAuthenticationService auth) Setup(string pin = "123456")
    {
        var pinSvc = new PinService();
        var auth = new PinAuthenticationService(pinSvc);
        var (hash, salt) = pinSvc.HashPin(pin);
        var emp = new Employee
        {
            Id = 1, FirstName = "Test", LastName = "User",
            PinHash = hash, PinSalt = salt,
            PinChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        return (emp, auth);
    }

    [Fact]
    public void Authenticate_CorrectPin_ShouldSucceed()
    {
        var (emp, auth) = Setup("123456");

        var result = auth.Authenticate(emp, "123456");

        result.Success.Should().BeTrue();
        emp.FailedPinAttempts.Should().Be(0);
        emp.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void Authenticate_WrongPin_ShouldFail()
    {
        var (emp, auth) = Setup("123456");

        var result = auth.Authenticate(emp, "000000");

        result.Success.Should().BeFalse();
        emp.FailedPinAttempts.Should().Be(1);
    }

    [Fact]
    public void Authenticate_FiveWrongPins_ShouldLock60Seconds()
    {
        var (emp, auth) = Setup("123456");

        for (int i = 0; i < 5; i++)
            auth.Authenticate(emp, "000000");

        emp.LockedUntil.Should().NotBeNull();
        emp.LockedUntil.Should().BeAfter(DateTime.UtcNow.AddSeconds(50));
        emp.LockedUntil.Should().BeBefore(DateTime.UtcNow.AddSeconds(65));
    }

    [Fact]
    public void Authenticate_AfterLockExpires_SixthWrongPin_ShouldLock5Minutes()
    {
        var (emp, auth) = Setup("123456");

        // 5 wrong pins -> 60s lock
        for (int i = 0; i < 5; i++)
            auth.Authenticate(emp, "000000");

        // Simulate lock expired
        emp.LockedUntil = DateTime.UtcNow.AddSeconds(-1);

        // 6th wrong pin -> 5 min lock
        auth.Authenticate(emp, "000000");

        emp.LockedUntil.Should().NotBeNull();
        emp.LockedUntil.Should().BeAfter(DateTime.UtcNow.AddMinutes(4));
        emp.LockedUntil.Should().BeBefore(DateTime.UtcNow.AddMinutes(6));
    }

    [Fact]
    public void Authenticate_AfterBothLocks_SeventhWrongPin_ShouldRequireAdminReset()
    {
        var (emp, auth) = Setup("123456");

        // 5 wrong pins -> 60s lock
        for (int i = 0; i < 5; i++)
            auth.Authenticate(emp, "000000");

        // Simulate 60s lock expired
        emp.LockedUntil = DateTime.UtcNow.AddSeconds(-1);

        // 6th wrong pin -> 5min lock
        auth.Authenticate(emp, "000000");

        // Simulate 5min lock expired
        emp.LockedUntil = DateTime.UtcNow.AddSeconds(-1);

        // 7th wrong pin -> RequiresAdminReset
        var result = auth.Authenticate(emp, "000000");

        result.RequiresAdminReset.Should().BeTrue();
    }

    [Fact]
    public void Authenticate_LockedAccount_ShouldReturnLockedStatus()
    {
        var (emp, auth) = Setup("123456");
        emp.LockedUntil = DateTime.UtcNow.AddMinutes(2);
        emp.FailedPinAttempts = 5;

        var result = auth.Authenticate(emp, "123456"); // Correct pin, but locked

        result.Success.Should().BeFalse();
        result.LockoutRemainingSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Authenticate_SuccessAfterFailure_ShouldResetCounter()
    {
        var (emp, auth) = Setup("123456");

        auth.Authenticate(emp, "000000");
        auth.Authenticate(emp, "000000");
        auth.Authenticate(emp, "123456"); // Correct

        emp.FailedPinAttempts.Should().Be(0);
        emp.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void ResetPin_ShouldGenerateNewPinAndClearLockout()
    {
        var (emp, auth) = Setup("123456");
        emp.FailedPinAttempts = 7;
        emp.LockedUntil = DateTime.UtcNow.AddMinutes(5);

        var newPin = auth.ResetPin(emp);

        newPin.Should().HaveLength(6);
        emp.FailedPinAttempts.Should().Be(0);
        emp.LockedUntil.Should().BeNull();
        emp.PinChangedAt.Should().BeNull(); // Forces re-change

        // New pin should work
        var result = auth.Authenticate(emp, newPin);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void NeedsPinChange_WhenPinChangedAtIsNull_ShouldReturnTrue()
    {
        var (emp, auth) = Setup();
        emp.PinChangedAt = null;

        auth.NeedsPinChange(emp).Should().BeTrue();
    }

    [Fact]
    public void NeedsPinChange_WhenPinWasChanged_ShouldReturnFalse()
    {
        var (emp, auth) = Setup();
        emp.PinChangedAt = DateTime.UtcNow;

        auth.NeedsPinChange(emp).Should().BeFalse();
    }
}
