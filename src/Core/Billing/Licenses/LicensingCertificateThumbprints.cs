using Bit.Core.Utilities;

namespace Bit.Core.Billing.Licenses;

/// <summary>
/// The licensing certificate thumbprints Bitwarden trusts to sign and verify license tokens. Any
/// certificate used to mint a license must match one of these, otherwise self-hosted instances reject
/// the resulting token at validation.
/// </summary>
public static class LicensingCertificateThumbprints
{
    public static readonly string Production =
        CoreHelpers.CleanCertificateThumbprint("B34876439FCDA2846505B2EFBBA6C4A951313EBE");

    public static readonly string Development =
        CoreHelpers.CleanCertificateThumbprint("207E64A231E8AA32AAF68A61037C075EBEBD553F");

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Production, Development };

    public static bool IsAllowed(string? thumbprint) =>
        !string.IsNullOrWhiteSpace(thumbprint) &&
        All.Contains(CoreHelpers.CleanCertificateThumbprint(thumbprint));
}
