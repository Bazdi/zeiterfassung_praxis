namespace Zeiterfassung.Web.Services;

/// <summary>
/// Per-connection PIN-authenticated employee session for the SelfService
/// pages. Lives for the lifetime of a Blazor circuit (Scoped). After
/// <see cref="IdleTimeout"/> of inactivity the session expires and the
/// user must re-enter their PIN.
///
/// Admin users (cookie auth) do NOT use this — they go straight through
/// the gate; this service is only consulted when no admin cookie is
/// present.
/// </summary>
public class EmployeeSessionService
{
    /// <summary>How long a PIN-authenticated employee stays signed in without activity.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);

    private int? _employeeId;
    private DateTime _lastActivityUtc = DateTime.UtcNow;

    /// <summary>
    /// Returns the currently PIN-authenticated employee id, or null if
    /// no PIN-auth session exists OR the session has expired due to
    /// idleness.
    /// </summary>
    public int? AuthenticatedEmployeeId
    {
        get
        {
            if (_employeeId is null) return null;
            if (DateTime.UtcNow - _lastActivityUtc > IdleTimeout)
            {
                _employeeId = null;
                return null;
            }
            return _employeeId;
        }
    }

    public bool IsAuthenticated => AuthenticatedEmployeeId is not null;

    public void SignIn(int employeeId)
    {
        _employeeId = employeeId;
        _lastActivityUtc = DateTime.UtcNow;
    }

    public void SignOut() => _employeeId = null;

    /// <summary>Bump the activity timestamp — call after each user interaction.</summary>
    public void Touch()
    {
        if (_employeeId is not null) _lastActivityUtc = DateTime.UtcNow;
    }
}
