using System.Security.Cryptography.X509Certificates;

namespace Bit.SharedWeb.Utilities;

/// <summary>
/// The certificates that one role loaded at startup. The host logs this report at startup. <see cref="Active"/> is
/// null when the role has no certificate. <see cref="Retired"/> holds the validation or decryption certificates.
/// <see cref="IsFallback"/> is true when the role uses the Identity certificates because it has no active source of
/// its own.
/// </summary>
public sealed record CertificateSourceReport(
    CertificateRole Role,
    X509Certificate2? Active,
    IReadOnlyList<X509Certificate2> Staged,
    IReadOnlyList<X509Certificate2> Retired,
    bool IsFallback);
