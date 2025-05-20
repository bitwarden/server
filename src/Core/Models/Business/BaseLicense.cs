using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Licenses.Attributes;
using Bit.Core.Enums;

namespace Bit.Core.Models.Business;

public abstract class BaseLicense : ILicense
{
    [LicenseVersion(1)]
    public string LicenseKey { get; set; }

    [LicenseVersion(1)]
    public Guid Id { get; set; }

    [LicenseVersion(1)]
    public string Name { get; set; }

    [LicenseVersion(1)]
    public int Version { get; set; }

    [LicenseIgnore(includeInHash: false)]
    public DateTime Issued { get; set; }

    [LicenseIgnore(includeInHash: false)]
    public DateTime? Refresh { get; set; }

    [LicenseVersion(1)]
    public DateTime? Expires { get; set; }

    [LicenseVersion(1)]
    public bool Trial { get; set; }

    [LicenseIgnore]
    public LicenseType? LicenseType { get; set; }

    [LicenseIgnore(includeInHash: false)]
    public string Hash { get; set; }

    [LicenseVersion(1)]
    [LicenseIgnore]
    public string Signature { get; set; }

    [LicenseIgnore]
    public string Token { get; set; }

    [JsonIgnore]
    [LicenseIgnore]
    public byte[] SignatureBytes => Convert.FromBase64String(Signature);

    public abstract byte[] GetDataBytes(bool forHash = false);

    public byte[] ComputeHash()
    {
        using (var alg = SHA256.Create())
        {
            return alg.ComputeHash(GetDataBytes(true));
        }
    }

    public bool VerifySignature(X509Certificate2 certificate)
    {
        using (var rsa = certificate.GetRSAPublicKey())
        {
            return rsa.VerifyData(GetDataBytes(), SignatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    public byte[] Sign(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("You don't have the private key!");
        }

        using (var rsa = certificate.GetRSAPrivateKey())
        {
            return rsa.SignData(GetDataBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }
}
