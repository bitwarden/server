using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Services;
using System;
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
                billingInfo.Subscription.TrialEndDate.Value < DateTime.UtcNow)
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

            Signature = Convert.ToBase64String(licenseService.SignLicense(this));
        }

        public string LicenseKey { get; set; }
        public Guid InstallationId { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
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
        public string Signature { get; set; }
        public byte[] SignatureBytes => Convert.FromBase64String(Signature);

        public byte[] GetSignatureData()
        {
            string data = null;
            if(Version == 1)
            {
                data = string.Format("organization:{0}_{1}_{2}_{3}_{4}_{5}_{6}_{7}_{8}_{9}_{10}_{11}_{12}_{13}_{14}_{15}",
                    Version,
                    Utilities.CoreHelpers.ToEpocSeconds(Issued),
                    Refresh.HasValue ? Utilities.CoreHelpers.ToEpocSeconds(Refresh.Value).ToString() : null,
                    Expires.HasValue ? Utilities.CoreHelpers.ToEpocSeconds(Expires.Value).ToString() : null,
                    LicenseKey,
                    InstallationId,
                    Id,
                    Enabled,
                    PlanType,
                    Seats,
                    MaxCollections,
                    UseGroups,
                    UseDirectory,
                    UseTotp,
                    MaxStorageGb,
                    SelfHost);
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }

            return Encoding.UTF8.GetBytes(data);
        }

        public bool CanUse(Guid installationId)
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
                return InstallationId == installationId;
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }
        }

        public bool VerifyData(Organization organization)
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
                return rsa.VerifyData(GetSignatureData(), SignatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        public byte[] Sign(X509Certificate2 certificate)
        {
            throw new NotImplementedException();
        }
    }
}
