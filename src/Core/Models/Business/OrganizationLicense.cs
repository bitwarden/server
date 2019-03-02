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

        public OrganizationLicense(Organization org, SubscriptionInfo subscriptionInfo, Guid installationId,
            ILicensingService licenseService)
        {
            Version = 4; // TODO: Version 5 bump
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
            UseEvents = org.UseEvents;
            UseDirectory = org.UseDirectory;
            UseTotp = org.UseTotp;
            Use2fa = org.Use2fa;
            UseApi = org.UseApi;
            MaxStorageGb = org.MaxStorageGb;
            SelfHost = org.SelfHost;
            UsersGetPremium = org.UsersGetPremium;
            Issued = DateTime.UtcNow;

            if(subscriptionInfo?.Subscription == null)
            {
                if(org.PlanType == PlanType.Custom && org.ExpirationDate.HasValue)
                {
                    Expires = Refresh = org.ExpirationDate.Value;
                    Trial = false;
                }
                else
                {
                    Expires = Refresh = Issued.AddDays(7);
                    Trial = true;
                }
            }
            else if(subscriptionInfo.Subscription.TrialEndDate.HasValue &&
                subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow)
            {
                Expires = Refresh = subscriptionInfo.Subscription.TrialEndDate.Value;
                Trial = true;
            }
            else
            {
                if(org.ExpirationDate.HasValue && org.ExpirationDate.Value < DateTime.UtcNow)
                {
                    // expired
                    Expires = Refresh = org.ExpirationDate.Value;
                }
                else if(subscriptionInfo?.Subscription?.PeriodDuration != null &&
                    subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180))
                {
                    Refresh = DateTime.UtcNow.AddDays(30);
                    Expires = subscriptionInfo?.Subscription.PeriodEndDate.Value.AddDays(60);
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
        public bool UseEvents { get; set; }
        public bool UseDirectory { get; set; }
        public bool UseTotp { get; set; }
        public bool Use2fa { get; set; }
        public bool UseApi { get; set; }
        public short? MaxStorageGb { get; set; }
        public bool SelfHost { get; set; }
        public bool UsersGetPremium { get; set; }
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
            if(Version >= 1 && Version <= 5)
            {
                var props = typeof(OrganizationLicense)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p =>
                        !p.Name.Equals(nameof(Signature)) &&
                        !p.Name.Equals(nameof(SignatureBytes)) &&
                        // UsersGetPremium was added in Version 2
                        (Version >= 2 || !p.Name.Equals(nameof(UsersGetPremium))) &&
                        // UseEvents was added in Version 3
                        (Version >= 3 || !p.Name.Equals(nameof(UseEvents))) &&
                        // Use2fa was added in Version 4
                        (Version >= 4 || !p.Name.Equals(nameof(Use2fa))) &&
                        // UseApi was added in Version 5
                        (Version >= 5 || !p.Name.Equals(nameof(UseApi))) &&
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

        public bool CanUse(GlobalSettings globalSettings)
        {
            if(!Enabled || Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
            {
                return false;
            }

            if(Version >= 1 && Version <= 5)
            {
                return InstallationId == globalSettings.Installation.Id && SelfHost;
            }
            else
            {
                throw new NotSupportedException($"Version {Version} is not supported.");
            }
        }

        public bool VerifyData(Organization organization, GlobalSettings globalSettings)
        {
            if(Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
            {
                return false;
            }

            if(Version >= 1 && Version <= 5)
            {
                var valid =
                    globalSettings.Installation.Id == InstallationId &&
                    organization.LicenseKey != null && organization.LicenseKey.Equals(LicenseKey) &&
                    organization.Enabled == Enabled &&
                    organization.PlanType == PlanType &&
                    organization.Seats == Seats &&
                    organization.MaxCollections == MaxCollections &&
                    organization.UseGroups == UseGroups &&
                    organization.UseDirectory == UseDirectory &&
                    organization.UseTotp == UseTotp &&
                    organization.SelfHost == SelfHost &&
                    organization.Name.Equals(Name);

                if(valid && Version >= 2)
                {
                    valid = organization.UsersGetPremium == UsersGetPremium;
                }

                if(valid && Version >= 3)
                {
                    valid = organization.UseEvents == UseEvents;
                }

                if(valid && Version >= 4)
                {
                    valid = organization.Use2fa == Use2fa;
                }

                if(valid && Version >= 5)
                {
                    valid = organization.UseApi == UseApi;
                }

                return valid;
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
