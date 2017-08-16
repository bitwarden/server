using Bit.Core.Models.Table;
using Bit.Core.Services;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Bit.Core.Models.Business
{
    public class UserLicense : ILicense
    {
        public UserLicense()
        { }

        public UserLicense(User user, BillingInfo billingInfo, ILicensingService licenseService)
        {
            LicenseKey = user.LicenseKey;
            Id = user.Id;
            Name = user.Name;
            Email = user.Email;
            Version = 1;
            Premium = user.Premium;
            MaxStorageGb = user.MaxStorageGb;
            Issued = DateTime.UtcNow;
            Expires = billingInfo?.UpcomingInvoice?.Date?.AddDays(7);
            Refresh = billingInfo?.UpcomingInvoice?.Date;
            Trial = (billingInfo?.Subscription?.TrialEndDate.HasValue ?? false) &&
                billingInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow;
            Signature = Convert.ToBase64String(licenseService.SignLicense(this));
        }

        public string LicenseKey { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool Premium { get; set; }
        public short? MaxStorageGb { get; set; }
        public int Version { get; set; }
        public DateTime Issued { get; set; }
        public DateTime? Refresh { get; set; }
        public DateTime? Expires { get; set; }
        public bool Trial { get; set; }
        public string Signature { get; set; }
        [JsonIgnore]
        public byte[] SignatureBytes => Convert.FromBase64String(Signature);

        public byte[] GetSignatureData()
        {
            string data = null;
            if(Version == 1)
            {
                var props = typeof(UserLicense)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => !p.Name.Equals(nameof(Signature)) && !p.Name.Equals(nameof(SignatureBytes)))
                    .OrderBy(p => p.Name)
                    .Select(p => $"{p.Name}:{Utilities.CoreHelpers.FormatLicenseSignatureValue(p.GetValue(this, null))}")
                    .Aggregate((c, n) => $"{c}|{n}");
                data = $"license:user|{props}";
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }

            return Encoding.UTF8.GetBytes(data);
        }

        public bool CanUse(User user)
        {
            if(Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
            {
                return false;
            }

            if(Version == 1)
            {
                return user.EmailVerified && user.Email.Equals(Email, StringComparison.InvariantCultureIgnoreCase);
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }
        }

        public bool VerifyData(User user)
        {
            if(Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
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

        public byte[] Sign(X509Certificate2 certificate)
        {
            if(!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("You don't have the private key!");
            }

            using(var rsa = certificate.GetRSAPrivateKey())
            {
                return rsa.SignData(GetSignatureData(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }
    }
}
