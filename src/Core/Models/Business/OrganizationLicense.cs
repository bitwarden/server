using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.Models.Business;

public class OrganizationLicense : ILicense
{
    public OrganizationLicense()
    { }

    public OrganizationLicense(Organization org, SubscriptionInfo subscriptionInfo, Guid installationId,
        ILicensingService licenseService, int? version = null)
    {
        Version = version.GetValueOrDefault(CURRENT_LICENSE_FILE_VERSION); // TODO: Remember to change the constant
        LicenseType = Enums.LicenseType.Organization;
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
        UsePolicies = org.UsePolicies;
        UseSso = org.UseSso;
        UseKeyConnector = org.UseKeyConnector;
        UseScim = org.UseScim;
        UseGroups = org.UseGroups;
        UseEvents = org.UseEvents;
        UseDirectory = org.UseDirectory;
        UseTotp = org.UseTotp;
        Use2fa = org.Use2fa;
        UseApi = org.UseApi;
        UseResetPassword = org.UseResetPassword;
        MaxStorageGb = org.MaxStorageGb;
        SelfHost = org.SelfHost;
        UsersGetPremium = org.UsersGetPremium;
        UseCustomPermissions = org.UseCustomPermissions;
        Issued = DateTime.UtcNow;

        if (subscriptionInfo?.Subscription == null)
        {
            if (org.PlanType == PlanType.Custom && org.ExpirationDate.HasValue)
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
        else if (subscriptionInfo.Subscription.TrialEndDate.HasValue &&
            subscriptionInfo.Subscription.TrialEndDate.Value > DateTime.UtcNow)
        {
            Expires = Refresh = subscriptionInfo.Subscription.TrialEndDate.Value;
            Trial = true;
        }
        else
        {
            if (org.ExpirationDate.HasValue && org.ExpirationDate.Value < DateTime.UtcNow)
            {
                // expired
                Expires = Refresh = org.ExpirationDate.Value;
            }
            else if (subscriptionInfo?.Subscription?.PeriodDuration != null &&
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
    public int? Seats { get; set; }
    public short? MaxCollections { get; set; }
    public bool UsePolicies { get; set; }
    public bool UseSso { get; set; }
    public bool UseKeyConnector { get; set; }
    public bool UseScim { get; set; }
    public bool UseGroups { get; set; }
    public bool UseEvents { get; set; }
    public bool UseDirectory { get; set; }
    public bool UseTotp { get; set; }
    public bool Use2fa { get; set; }
    public bool UseApi { get; set; }
    public bool UseResetPassword { get; set; }
    public short? MaxStorageGb { get; set; }
    public bool SelfHost { get; set; }
    public bool UsersGetPremium { get; set; }
    public bool UseCustomPermissions { get; set; }
    public int Version { get; set; }
    public DateTime Issued { get; set; }
    public DateTime? Refresh { get; set; }
    public DateTime? Expires { get; set; }
    public bool Trial { get; set; }
    public LicenseType? LicenseType { get; set; }
    public string Hash { get; set; }
    public string Signature { get; set; }
    [JsonIgnore]
    public byte[] SignatureBytes => Convert.FromBase64String(Signature);

    /// <summary>
    /// Represents the current version of the license format. Should be updated whenever new fields are added.
    /// </summary>
    private const int CURRENT_LICENSE_FILE_VERSION = 10;
    private bool ValidLicenseVersion
    {
        get => Version is >= 1 and <= 11;
    }

    public byte[] GetDataBytes(bool forHash = false)
    {
        string data = null;
        if (ValidLicenseVersion)
        {
            var props = typeof(OrganizationLicense)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    !p.Name.Equals(nameof(Signature)) &&
                    !p.Name.Equals(nameof(SignatureBytes)) &&
                    !p.Name.Equals(nameof(LicenseType)) &&
                    // UsersGetPremium was added in Version 2
                    (Version >= 2 || !p.Name.Equals(nameof(UsersGetPremium))) &&
                    // UseEvents was added in Version 3
                    (Version >= 3 || !p.Name.Equals(nameof(UseEvents))) &&
                    // Use2fa was added in Version 4
                    (Version >= 4 || !p.Name.Equals(nameof(Use2fa))) &&
                    // UseApi was added in Version 5
                    (Version >= 5 || !p.Name.Equals(nameof(UseApi))) &&
                    // UsePolicies was added in Version 6
                    (Version >= 6 || !p.Name.Equals(nameof(UsePolicies))) &&
                    // UseSso was added in Version 7
                    (Version >= 7 || !p.Name.Equals(nameof(UseSso))) &&
                    // UseResetPassword was added in Version 8
                    (Version >= 8 || !p.Name.Equals(nameof(UseResetPassword))) &&
                    // UseKeyConnector was added in Version 9
                    (Version >= 9 || !p.Name.Equals(nameof(UseKeyConnector))) &&
                    // UseScim was added in Version 10
                    (Version >= 10 || !p.Name.Equals(nameof(UseScim))) &&
                    // UseCustomPermissions was added in Version 11
                    (Version >= 11 || !p.Name.Equals(nameof(UseCustomPermissions))) &&
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
        using (var alg = SHA256.Create())
        {
            return alg.ComputeHash(GetDataBytes(true));
        }
    }

    public bool ValidateForInstallation(IGlobalSettings globalSettings, ILicensingService licensingService, out string exception)
    {
        if (!Enabled || Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
        {
            exception = "Invalid license. Your organization is disabled or the license has expired.";
            return false;
        }

        if (!ValidLicenseVersion)
        {
            exception = $"Version {Version} is not supported.";
            return false;
        }

        if (InstallationId != globalSettings.Installation.Id || !SelfHost)
        {
            exception = "Invalid license. Make sure your license allows for on-premise " +
                "hosting of organizations and that the installation id matches your current installation.";
            return false;
        }

        if (LicenseType != null && LicenseType != Enums.LicenseType.Organization)
        {
            exception = "Premium licenses cannot be applied to an organization. "
                                          + "Upload this license from your personal account settings page.";
            return false;
        }

        if (!licensingService.VerifyLicense(this))
        {
            exception = "Invalid license.";
            return false;
        }

        exception = "";
        return true;
    }

    public bool ValidateForOrganization(SelfHostedOrganizationDetails organization, out string exception)
    {
        if (Seats.HasValue && organization.OccupiedSeatCount > Seats.Value)
        {
            exception = $"Your organization currently has {organization.OccupiedSeatCount} seats filled. " +
                $"Your new license only has ({Seats.Value}) seats. Remove some users.";
            return false;
        }

        if (MaxCollections.HasValue && organization.CollectionCount > MaxCollections.Value)
        {
            exception = $"Your organization currently has {organization.CollectionCount} collections. " +
                $"Your new license allows for a maximum of ({MaxCollections.Value}) collections. " +
                "Remove some collections.";
            return false;
        }

        if (!UseGroups && organization.UseGroups && organization.GroupCount > 1)
        {
            exception = $"Your organization currently has {organization.GroupCount} groups. " +
                $"Your new license does not allow for the use of groups. Remove all groups.";
            return false;
        }

        var enabledPolicyCount = organization.Policies.Count(p => p.Enabled);
        if (!UsePolicies && organization.UsePolicies && enabledPolicyCount > 0)
        {
            exception = $"Your organization currently has {enabledPolicyCount} enabled " +
                $"policies. Your new license does not allow for the use of policies. Disable all policies.";
            return false;
        }

        if (!UseSso && organization.UseSso && organization.SsoConfig is { Enabled: true })
        {
            exception = $"Your organization currently has a SSO configuration. " +
                $"Your new license does not allow for the use of SSO. Disable your SSO configuration.";
            return false;
        }

        if (!UseKeyConnector && organization.UseKeyConnector && organization.SsoConfig != null && organization.SsoConfig.GetData().KeyConnectorEnabled)
        {
            exception = $"Your organization currently has Key Connector enabled. " +
                $"Your new license does not allow for the use of Key Connector. Disable your Key Connector.";
            return false;
        }

        if (!UseScim && organization.UseScim && organization.ScimConnections != null &&
            organization.ScimConnections.Any(c => c.GetConfig<ScimConfig>() is { Enabled: true }))
        {
            exception = "Your new plan does not allow the SCIM feature. " +
                "Disable your SCIM configuration.";
            return false;
        }

        if (!UseCustomPermissions && organization.UseCustomPermissions &&
            organization.OrganizationUsers.Any(ou => ou.Type == OrganizationUserType.Custom))
        {
            exception = "Your new plan does not allow the Custom Permissions feature. " +
                "Disable your Custom Permissions configuration.";
            return false;
        }

        if (!UseResetPassword && organization.UseResetPassword &&
            organization.Policies.Any(p => p.Type == PolicyType.ResetPassword && p.Enabled))
        {
            exception = "Your new license does not allow the Password Reset feature. "
                + "Disable your Password Reset policy.";
            return false;
        }

        exception = "";
        return true;
    }

    public bool VerifyData(Organization organization, IGlobalSettings globalSettings)
    {
        if (Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
        {
            return false;
        }

        if (ValidLicenseVersion)
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

            if (valid && Version >= 2)
            {
                valid = organization.UsersGetPremium == UsersGetPremium;
            }

            if (valid && Version >= 3)
            {
                valid = organization.UseEvents == UseEvents;
            }

            if (valid && Version >= 4)
            {
                valid = organization.Use2fa == Use2fa;
            }

            if (valid && Version >= 5)
            {
                valid = organization.UseApi == UseApi;
            }

            if (valid && Version >= 6)
            {
                valid = organization.UsePolicies == UsePolicies;
            }

            if (valid && Version >= 7)
            {
                valid = organization.UseSso == UseSso;
            }

            if (valid && Version >= 8)
            {
                valid = organization.UseResetPassword == UseResetPassword;
            }

            if (valid && Version >= 9)
            {
                valid = organization.UseKeyConnector == UseKeyConnector;
            }

            if (valid && Version >= 10)
            {
                valid = organization.UseScim == UseScim;
            }

            if (valid && Version >= 11)
            {
                valid = organization.UseCustomPermissions == UseCustomPermissions;
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

public class OrganizationLicenseMapperProfile : Profile
{
    public OrganizationLicenseMapperProfile()
    {
        // Do not update Organization.Id from the license
        // The same Org will have different Ids between Cloud and Selfhosted
        ShouldMapField = fieldInfo => false;
        ShouldMapProperty = propertyInfo => propertyInfo.Name != "Id";

        CreateMap<OrganizationLicense, Organization>();
    }
}
