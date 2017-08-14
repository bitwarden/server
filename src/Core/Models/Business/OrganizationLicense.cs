using Bit.Core.Enums;
using Bit.Core.Models.Table;
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

        public OrganizationLicense(Organization org, Guid installationId)
        {
            LicenseKey = "";
            InstallationId = installationId;
            Id = org.Id;
            Name = org.Name;
            Enabled = org.Enabled;
            Seats = org.Seats;
            MaxCollections = org.MaxCollections;
            UseGroups = org.UseGroups;
            UseDirectory = org.UseDirectory;
            UseTotp = org.UseTotp;
            MaxStorageGb = org.MaxStorageGb;
            SelfHost = org.SelfHost;
            Version = 1;
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
        public DateTime? Expires { get; set; }
        public bool Trial { get; set; }
        public string Signature { get; set; }
        public byte[] SignatureBytes => Convert.FromBase64String(Signature);

        public byte[] GetSignatureData()
        {
            string data = null;
            if(Version == 1)
            {
                data = string.Format("organization:{0}_{1}_{2}_{3}_{4}_{5}_{6}_{7}_{8}_{9}_{10}_{11}_{12}_{13}_{14}",
                    Version,
                    Utilities.CoreHelpers.ToEpocSeconds(Issued),
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
