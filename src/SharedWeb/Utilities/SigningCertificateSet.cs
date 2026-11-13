using System.Security.Cryptography.X509Certificates;

namespace Bit.SharedWeb.Utilities;

/// <summary>
/// The certificates of one signing role. <see cref="Active"/> signs. The <see cref="Staged"/> and
/// <see cref="Validation"/> certificates are published for token validation only. <see cref="IsFallback"/> is true
/// when the role uses the Identity certificates because it has no active source of its own.
/// </summary>
public sealed record SigningCertificateSet(
    X509Certificate2? Active,
    IReadOnlyList<X509Certificate2> Staged,
    IReadOnlyList<X509Certificate2> Validation,
    bool IsFallback);
