using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.SharedWeb.Certificates;

/// <summary>
/// The certificates that the Identity settings configure in each slot. Each certificate loads on first read, at most
/// one time, so that each role that reads a certificate gets the same loaded object. A certificate that no role reads
/// does not load.
/// </summary>
public sealed class IdentityCertificateSlots
{
    private readonly Lazy<X509Certificate2?> _active;
    private readonly Lazy<IReadOnlyList<X509Certificate2>> _staged;
    private readonly Lazy<IReadOnlyList<X509Certificate2>> _retired;

    public IdentityCertificateSlots(GlobalSettings globalSettings)
    {
        var identityServer = globalSettings.IdentityServer;
        _active = new(() => CoreHelpers.GetIdentityServerCertificate(globalSettings));
        _staged = new(() =>
        {
            var staged = CoreHelpers.GetStagedCertificate(globalSettings, identityServer.FutureCertificate,
                "globalSettings:identityServer:futureCertificate");
            return staged == null ? [] : [staged];
        });
        _retired = new(() => CoreHelpers.GetRetiredCertificates(globalSettings,
            identityServer.ValidationCertificates, "globalSettings:identityServer:validationCertificates"));
    }

    /// <summary>
    /// The Identity active certificate, from <see cref="CoreHelpers.GetIdentityServerCertificate"/>. Null when the
    /// root has no source that loads.
    /// </summary>
    /// <exception cref="InvalidOperationException">The future slot is enabled and active, and its certificate did
    /// not load. Or the certificate does not have an RSA key.</exception>
    public X509Certificate2? Active => _active.Value;

    /// <summary>
    /// The Identity staged certificate, as a list of one. Empty when the future slot is not enabled, or is active.
    /// </summary>
    /// <exception cref="InvalidOperationException">The certificate did not load, or does not have an RSA key.
    /// </exception>
    public IReadOnlyList<X509Certificate2> Staged => _staged.Value;

    /// <summary>
    /// The Identity retired certificates: the certificates of the enabled validation entries, in the configured order.
    /// </summary>
    /// <exception cref="InvalidOperationException">A certificate did not load, or does not have an RSA key.
    /// </exception>
    public IReadOnlyList<X509Certificate2> Retired => _retired.Value;
}
