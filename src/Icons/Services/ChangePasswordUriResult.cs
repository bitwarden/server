namespace Bit.Icons.Services;

/// <summary>
/// The outcome of a well-known change-password lookup for a domain.
/// </summary>
public enum ChangePasswordUriResultType
{
    /// <summary>A reliable well-known change-password URL was found.</summary>
    Found,

    /// <summary>Probes completed; the domain does not reliably support the well-known URL. Definitive, safe to cache.</summary>
    NotSupported,

    /// <summary>A probe failed transiently (DNS, timeout, connection). Not definitive; must not be cached.</summary>
    LookupFailed,
}

/// <summary>
/// Outcome of a change-password lookup, distinguishing a definitive answer from a transient
/// failure so callers can decide what is safe to cache.
/// </summary>
public sealed class ChangePasswordUriResult
{
    public ChangePasswordUriResultType Type { get; }

    /// <summary>The change-password URL when <see cref="Type"/> is <see cref="ChangePasswordUriResultType.Found"/>; otherwise <c>null</c>.</summary>
    public string? Uri { get; }

    private ChangePasswordUriResult(ChangePasswordUriResultType type, string? uri)
    {
        Type = type;
        Uri = uri;
    }

    public static ChangePasswordUriResult Found(string uri) => new(ChangePasswordUriResultType.Found, uri);

    public static ChangePasswordUriResult NotSupported { get; } = new(ChangePasswordUriResultType.NotSupported, null);

    public static ChangePasswordUriResult LookupFailed { get; } = new(ChangePasswordUriResultType.LookupFailed, null);
}
