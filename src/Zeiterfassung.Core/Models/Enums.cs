namespace Zeiterfassung.Core.Models;

public enum TimeEntryType
{
    Kommen,
    Gehen,
    PauseStart,
    PauseEnd
}

public enum EntrySource
{
    Terminal,
    SelfService,
    Correction,
    Migration
}

public enum CorrectionStatus
{
    Open,
    Approved,
    Rejected
}

public enum LeaveType
{
    Urlaub,
    Krank,
    Sonstiges
}

public enum LeaveRequestStatus
{
    Open,
    Approved,
    Rejected
}
