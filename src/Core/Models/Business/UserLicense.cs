using Bit.Core.Models.Table;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Bit.Core.Models.Business
{
    public class UserLicense : ILicense
    {
        public UserLicense()
        { }

        public UserLicense(User user)
        {
            LicenseKey = "";
            Id = user.Id;
            Email = user.Email;
            Version = 1;
        }

        public string LicenseKey { get; set; }
        public Guid Id { get; set; }
        public string Email { get; set; }
        public bool Premium { get; set; }
        public short? MaxStorageGb { get; set; }
        public int Version { get; set; }
        public DateTime Issued { get; set; }
        public DateTime Expires { get; set; }
        public bool Trial { get; set; }
        public string Signature { get; set; }
        public byte[] SignatureBytes => Convert.FromBase64String(Signature);

        public byte[] GetSignatureData()
        {
            string data = null;
            if(Version == 1)
            {
                data = string.Format("user:{0}_{1}_{2}_{3}_{4}_{5}_{6}_{7}",
                    Version,
                    Utilities.CoreHelpers.ToEpocMilliseconds(Issued),
                    Utilities.CoreHelpers.ToEpocMilliseconds(Expires),
                    LicenseKey,
                    Id,
                    Email,
                    Premium,
                    MaxStorageGb);
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }

            return Encoding.UTF8.GetBytes(data);
        }

        public bool VerifyData(User user)
        {
            if(Issued > DateTime.UtcNow)
            {
                return false;
            }

            if(Expires < DateTime.UtcNow)
            {
                return false;
            }

            if(Version == 1)
            {
                return
                    user.LicenseKey.Equals(LicenseKey, StringComparison.InvariantCultureIgnoreCase) &&
                    user.Premium == Premium &&
                    user.Email.Equals(Email, StringComparison.InvariantCultureIgnoreCase);
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }
        }

        public bool VerifySignature(X509Certificate2 certificate)
        {
            using(var rsa = certificate.GetRSAPublicKey())
            {
                return rsa.VerifyData(GetSignatureData(), SignatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }
    }
}
