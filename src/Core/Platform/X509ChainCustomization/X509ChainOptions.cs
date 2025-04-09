#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Bit.Core.Platform.X509ChainCustomization;

/// <summary>
/// Allows for customization of the <see cref="X509ChainPolicy"/> and access to a custom server certificate validator
/// if customization has been made.
/// </summary>
public sealed class X509ChainOptions
{
    // This is the directory that we historically used to allow certificates be added inside our container
    // and then on start of the container we would move them to `/usr/local/share/ca-certificates/` and call
    // `update-ca-certificates` but since that operation requires root we can't do it in a rootless container.
    // Ref: https://github.com/bitwarden/server/blob/67d7d685a619a5fc413f8532dacb09681ee5c956/src/Api/entrypoint.sh#L38-L41
    public const string DefaultAdditionalCustomTrustCertificatesDirectory = "/etc/bitwarden/ca-certificates/";

    /// <summary>
    /// A directory where additional certificates should be read from and included in <see cref="X509ChainPolicy.CustomTrustStore"/>.
    /// </summary>
    /// <remarks>
    /// Only certificates suffixed with <c>*.crt</c> will be read. If <see cref="AdditionalCustomTrustCertificates"/> is
    /// set, then this directory will not be read from.
    /// </remarks>
    public string? AdditionalCustomTrustCertificatesDirectory { get; set; } = DefaultAdditionalCustomTrustCertificatesDirectory;

    /// <summary>
    /// A list of additional certificates that should be included in <see cref="X509ChainPolicy.CustomTrustStore"/>.
    /// </summary>
    /// <remarks>
    /// If this value is set manually, then <see cref="AdditionalCustomTrustCertificatesDirectory"/> will be ignored.
    /// </remarks>
    public List<X509Certificate2>? AdditionalCustomTrustCertificates { get; set; }

    /// <summary>
    /// Attempts to retrieve a custom remote certificate validation callback.
    /// </summary>
    /// <param name="callback"></param>
    /// <returns>Returns <see langword="true"/> when we have custom remote certification that should be added,
    /// <see langword="false"/> when no custom validation is needed and the default validation callback should
    /// be used instead.
    /// </returns>
    [MemberNotNullWhen(true, nameof(AdditionalCustomTrustCertificates))]
    public bool TryGetCustomRemoteCertificateValidationCallback(
        [MaybeNullWhen(false)] out Func<X509Certificate2?, X509Chain?, SslPolicyErrors, bool> callback)
    {
        callback = null;
        if (AdditionalCustomTrustCertificates == null || AdditionalCustomTrustCertificates.Count == 0)
        {
            return false;
        }

        // Do this outside of the callback so that we aren't opening the root store every request.
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine, OpenFlags.ReadOnly);
        var rootCertificates = store.Certificates;

        // Ref: https://github.com/dotnet/runtime/issues/39835#issuecomment-663020581
        callback = (certificate, chain, errors) =>
        {
            if (chain == null || certificate == null)
            {
                return false;
            }

            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;

            // We want our additional certificates to be in addition to the machines root store.
            chain.ChainPolicy.CustomTrustStore.AddRange(rootCertificates);

            foreach (var additionalCertificate in AdditionalCustomTrustCertificates)
            {
                chain.ChainPolicy.CustomTrustStore.Add(additionalCertificate);
            }
            return chain.Build(certificate);
        };
        return true;
    }
}
