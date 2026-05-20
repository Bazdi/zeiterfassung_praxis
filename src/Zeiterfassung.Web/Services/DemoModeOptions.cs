namespace Zeiterfassung.Web.Services;

/// <summary>
/// Marker indicating the app runs in DEMO mode.
/// Set via <c>--demo</c> CLI flag (or <c>ASPNETCORE_DEMO=true</c>).
///
/// When enabled:
///  - Uses a separate SQLite file (<c>zeiterfassung-demo.db</c>) — production
///    data is never touched.
///  - The DB is dropped and re-seeded on every startup so the demo is always
///    reproducible.
///  - A "DEMO" banner is rendered above the UI shell so nobody confuses the
///    sandbox with the real practice.
///  - Bound port defaults to 5001 (production exe binds 5000), so both can
///    run side-by-side.
/// </summary>
public class DemoModeOptions
{
    public bool Enabled { get; init; }

    /// <summary>
    /// Username of the seeded demo admin. Password is shown in the console
    /// banner so the operator can log in.
    /// </summary>
    public string AdminUsername { get; init; } = "demo";

    /// <summary>Password for the seeded demo admin.</summary>
    public string AdminPassword { get; init; } = "Demo12345!";

    public static DemoModeOptions FromArgs(string[] args)
    {
        var enabled =
            args.Any(a =>
                string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--mock", StringComparison.OrdinalIgnoreCase))
            || string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_DEMO"),
                "true", StringComparison.OrdinalIgnoreCase);

        return new DemoModeOptions { Enabled = enabled };
    }
}
