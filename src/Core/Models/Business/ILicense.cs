using System;
using System.Security.Cryptography.X509Certificates;

namespace Bit.Core.Models.Business
{
    public interface ILicense
    {
        string LicenseKey { get; set; }
        int Version { get; set; }
        DateTime Issued { get; set; }
        DateTime? Refresh { get; set; }
        DateTime? Expires { get; set; }
        bool Trial { get; set; }
        string Signature { get; set; }
        byte[] GetSignatureData();
        bool VerifySignature(X509Certificate2 certificate);
        byte[] Sign(X509Certificate2 certificate);
    }
}
