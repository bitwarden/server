using Bit.Core.Enums;
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
    public class OrganizationLicense : ILicense
    {
        public OrganizationLicense()
        { }

        public OrganizationLicense(Organization org, BillingInfo billingInfo, Guid installationId,
            ILicensingService licenseService)
        {
            Version = 1;
            LicenseKey = org.LicenseKey;
            InstallationId = installationId;
            Id = org.Id;
            Name = org.Name;
            BillingEmail = org.BillingEmail;
            BusinessName = org.BusinessName;
            Enabled = org.Enabled;
            Plan = org.Plan;
            PlanType = org.PlanType;
            Seats = org.Seats;
            MaxCollections = org.MaxCollections;
            UseGroups = org.UseGroups;
            UseDirectory = org.UseDirectory;
            UseTotp = org.UseTotp;
            MaxStorageGb = org.MaxStorageGb;
            SelfHost = org.SelfHost;
            Issued = DateTime.UtcNow;

            if(billingInfo?.Subscription == null)
            {
                Expires = Refresh = Issued.AddDays(7);
                Trial = true;
            }
            else if(billingInfo.Subscription.TrialEndDate.HasValue &&
                billingInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow)
            {
                Expires = Refresh = billingInfo.Subscription.TrialEndDate.Value;
                Trial = true;
            }
            else
            {
                if(org.ExpirationDate.HasValue && org.ExpirationDate.Value < DateTime.UtcNow)
                {
                    // expired
                    Expires = Refresh = org.ExpirationDate.Value;
                }
                else if(billingInfo?.Subscription?.PeriodDuration != null &&
                    billingInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180))
                {
                    Refresh = DateTime.UtcNow.AddDays(30);
                    Expires = billingInfo?.Subscription.PeriodEndDate.Value.AddDays(60);
                }
                else
                {
                    Expires = org.ExpirationDate.HasValue ? org.ExpirationDate.Value.AddMonths(11) : Issued.AddYears(1);
                    Refresh = DateTime.UtcNow - Expires > TimeSpan.FromDays(30) ? DateTime.UtcNow.AddDays(30) : Expires;
                }

                Trial = false;
            }

            Hash = Convert.ToBase64String(ComputeHash());
            Signature = Convert.ToBase64String(licenseService.SignLicense(this));
        }

        public string LicenseKey { get; set; }
        public Guid InstallationId { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string BillingEmail { get; set; }
        public string BusinessName { get; set; }
        public bool Enabled { get; set; }
        public string Plan { get; set; }
        public PlanType PlanType { get; set; }
        public short? Seats { get; set; }
        public short? MaxCollections { get; set; }
        public bool UseGroups { get; set; }
        public bool UseDirectory { get; set; }
        public bool UseTotp { get; set; }
        public short? MaxStorageGb { get; set; }
        public bool SelfHost { get; set; }
        public int Version { get; set; }
        public DateTime Issued { get; set; }
        public DateTime? Refresh { get; set; }
        public DateTime? Expires { get; set; }
        public bool Trial { get; set; }
        public string Hash { get; set; }
        public string Signature { get; set; }
        [JsonIgnore]
        public byte[] SignatureBytes => Convert.FromBase64String(Signature);

        public byte[] GetDataBytes(bool forHash = false)
        {
            string data = null;
            if(Version == 1)
            {
                var props = typeof(OrganizationLicense)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p =>
                        !p.Name.Equals(nameof(Signature)) &&
                        !p.Name.Equals(nameof(SignatureBytes)) &&
                        (
                            !forHash ||
                            (
                                !p.Name.Equals(nameof(Hash)) &&
                                !p.Name.Equals(nameof(Issued)) &&
                                !p.Name.Equals(nameof(Refresh))
                            )
                        ))
                    .OrderBy(p => p.Name)
                    .Select(p => $"{p.Name}:{Utilities.CoreHelpers.FormatLicenseSignatureValue(p.GetValue(this, null))}")
                    .Aggregate((c, n) => $"{c}|{n}");
                data = $"license:organization|{props}";
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }

            return Encoding.UTF8.GetBytes(data);
        }

        public byte[] ComputeHash()
        {
            using(var alg = SHA256.Create())
            {
                return alg.ComputeHash(GetDataBytes(true));
            }
        }

        public bool CanUse(Guid installationId)
        {
            if(!Enabled || Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
            {
                return false;
            }

            if(Version == 1)
            {
                return InstallationId == installationId && SelfHost;
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }
        }

        public bool VerifyData(Organization organization)
        {
            if(Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
            {
                return false;
            }

            if(Version == 1)
            {
                return
                    organization.LicenseKey.Equals(LicenseKey, StringComparison.InvariantCultureIgnoreCase) &&
                    organization.Enabled == Enabled &&
                    organization.PlanType == PlanType &&
                    organization.Seats == Seats &&
                    organization.MaxCollections == MaxCollections &&
                    organization.UseGroups == UseGroups &&
                    organization.UseDirectory == UseDirectory &&
                    organization.UseTotp == UseTotp &&
                    organization.SelfHost == SelfHost;
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
                return rsa.VerifyData(GetDataBytes(), SignatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
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
                return rsa.SignData(GetDataBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }
    }
}
