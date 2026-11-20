namespace Bit.Core.Utilities;

/// <summary>
/// Process-wide defaults for <see cref="System.Text.RegularExpressions.Regex"/>.
/// </summary>
/// <remarks>
/// Deliberately kept free of any static <c>Regex</c> fields: the default match timeout is read once,
/// when the <c>Regex</c> type is first initialized, so it must be configured before any regular
/// expression is constructed anywhere in the process.
/// </remarks>
public static class RegexDefaults
{
    /// <summary>
    /// The timeout applied to every <c>Regex</c> that does not specify its own.
    /// </summary>
    public static readonly TimeSpan DefaultMatchTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Configures a process-wide default <c>Regex</c> match timeout as defence in depth against
    /// catastrophic backtracking on untrusted input (CWE-1333). Call this as the very first
    /// statement of <c>Main</c>, before anything else has a chance to construct a <c>Regex</c>.
    /// </summary>
    public static void ConfigureDefaultMatchTimeout()
    {
        AppContext.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", DefaultMatchTimeout);
    }
}
