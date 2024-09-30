using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Attributes;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Models.Business;

public class OrganizationLicense : ILicense
{
    /// <summary>
    /// Represents the current version of the license format. Previously, the engineer who adds a property to this class
    /// would need to increment this constant. Now, if you add a property, you must simply add a new claim
    /// </summary>
    /// <remarks>Intentionally set one version behind to allow self-hosted users some time to update before
    /// getting out of date license errors</remarks>
    public const int CurrentLicenseFileVersion = 14;

    public OrganizationLicense()
    {
    }

    public OrganizationLicense(
        Organization org,
        SubscriptionInfo subscriptionInfo,
        Guid installationId,
        ILicensingService licenseService,
        int? version = null)
    {
        Version = version.GetValueOrDefault(CurrentLicenseFileVersion);
        LicenseType = Core.Enums.LicenseType.Organization;
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
        UsePasswordManager = org.UsePasswordManager;
        UseSecretsManager = org.UseSecretsManager;
        SmSeats = org.SmSeats;
        SmServiceAccounts = org.SmServiceAccounts;
        LimitCollectionCreationDeletion = org.LimitCollectionCreationDeletion;
        AllowAdminAccessToAllCollectionItems = org.AllowAdminAccessToAllCollectionItems;

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
                Expires = subscriptionInfo.Subscription.PeriodEndDate?.AddDays(Core.Constants
                    .OrganizationSelfHostSubscriptionGracePeriodDays);
                ExpirationWithoutGracePeriod = subscriptionInfo.Subscription.PeriodEndDate;
            }
            else
            {
                Expires = org.ExpirationDate?.AddMonths(11) ?? Issued.AddYears(1);
                Refresh = DateTime.UtcNow - Expires > TimeSpan.FromDays(30) ? DateTime.UtcNow.AddDays(30) : Expires;
            }

            Trial = false;
        }

        Hash = Convert.ToBase64String(EncodedHash); // Hash must come after all properties are set, and before Signature
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
        Token = licenseService.GenerateToken(this);
    }

    [LicenseVersion(1)]
    public string LicenseKey { get; set; }

    [LicenseVersion(1)]
    public Guid InstallationId { get; set; }

    [LicenseVersion(1)]
    public Guid Id { get; set; }

    [LicenseVersion(1)]
    public string Name { get; set; }

    [LicenseVersion(1)]
    public string BillingEmail { get; set; }

    [LicenseVersion(1)]
    public string BusinessName { get; set; }

    [LicenseVersion(1)]
    public bool Enabled { get; set; }

    [LicenseVersion(1)]
    public string Plan { get; set; }

    [LicenseVersion(1)]
    public PlanType PlanType { get; set; }

    [LicenseVersion(1)]
    public int? Seats { get; set; }

    [LicenseVersion(1)]
    public short? MaxCollections { get; set; }

    [LicenseVersion(1)]
    public bool UseGroups { get; set; }

    [LicenseVersion(1)]
    public bool UseDirectory { get; set; }

    [LicenseVersion(1)]
    public bool UseTotp { get; set; }

    [LicenseVersion(1)]
    public short? MaxStorageGb { get; set; }

    [LicenseVersion(1)]
    public bool SelfHost { get; set; }

    [LicenseVersion(1)]
    public int Version { get; set; }

    [LicenseVersion(1)]
    public DateTime? Expires { get; set; }

    [LicenseVersion(1)]
    public bool Trial { get; set; }

    [LicenseVersion(2)]
    public bool UsersGetPremium { get; set; }

    [LicenseVersion(3)]
    public bool UseEvents { get; set; }

    [LicenseVersion(4)]
    public bool Use2fa { get; set; }

    [LicenseVersion(5)]
    public bool UseApi { get; set; }

    [LicenseVersion(6)]
    public bool UsePolicies { get; set; }

    [LicenseVersion(7)]
    public bool UseSso { get; set; }

    [LicenseVersion(8)]
    public bool UseResetPassword { get; set; }

    [LicenseVersion(9)]
    public bool UseKeyConnector { get; set; }

    [LicenseVersion(10)]
    public bool UseScim { get; set; }

    [LicenseVersion(11)]
    public bool UseCustomPermissions { get; set; }

    [LicenseVersion(12)]
    public DateTime? ExpirationWithoutGracePeriod { get; set; }

    [LicenseVersion(13)]
    public bool UsePasswordManager { get; set; }

    [LicenseVersion(13)]
    public bool UseSecretsManager { get; set; }

    [LicenseVersion(13)]
    public int? SmSeats { get; set; }

    [LicenseVersion(13)]
    public int? SmServiceAccounts { get; set; }

    [LicenseVersion(14)]
    public bool LimitCollectionCreationDeletion { get; set; } = true;

    [LicenseVersion(15)]
    public bool AllowAdminAccessToAllCollectionItems { get; set; } = true;

    [LicenseIgnore]
    public LicenseType? LicenseType { get; set; }

    [LicenseIgnore]
    public string Signature { get; set; }

    [LicenseIgnore]
    public string Token { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public string Hash { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public DateTime Issued { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public DateTime? Refresh { get; set; }

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] SignatureBytes => Convert.FromBase64String(Signature);

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedData => this.EncodeLicense(p => p.ShouldIncludePropertyOnLicense(Version));

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedHash =>
        SHA256.HashData(
            this.EncodeLicense(p => p.ShouldIncludePropertyOnLicense(Version, LicenseIgnoreCondition.OnHash)));

    public bool ValidLicenseVersion => Version is >= 1 and <= CurrentLicenseFileVersion + 1;

    public bool VerifyData(Organization organization, IGlobalSettings globalSettings)
    {
        if (Issued > DateTime.UtcNow || Expires < DateTime.UtcNow)
        {
            return false;
        }

        if (!ValidLicenseVersion)
        {
            throw new NotSupportedException($"Version {Version} is not supported.");
        }

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

        /*
         * Version 12 added ExpirationWithoutDatePeriod, but that property is informational only and is not saved to the
         * Organization object. It's validated as part of the hash but does not need to be validated here.
         */
        if (valid && Version >= 13)
        {
            valid = organization.UseSecretsManager == UseSecretsManager &&
                    organization.UsePasswordManager == UsePasswordManager &&
                    organization.SmSeats == SmSeats &&
                    organization.SmServiceAccounts == SmServiceAccounts;
        }

        /*
         * Version 14 added LimitCollectionCreationDeletion and Version 15 added AllowAdminAccessToAllCollectionItems,
         * however these are just user settings, and it is not worth failing validation if they mismatch.
         * They are intentionally excluded.
         */

        return valid;
    }

    /// <summary>
    /// Converts the license to a JWT string using the RsaSha256Signature algorithm
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public string ToToken(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("You don't have the private key!");
        }

        using var rsa = certificate.GetRSAPrivateKey();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(nameof(LicenseKey), LicenseKey),
                new Claim(nameof(InstallationId), InstallationId.ToString()),
                new Claim(nameof(Id), Id.ToString()),
                new Claim(nameof(Name), Name),
                new Claim(nameof(BillingEmail), BillingEmail),
                new Claim(nameof(BusinessName), BusinessName ?? string.Empty),
                new Claim(nameof(Enabled), Enabled.ToString()),
                new Claim(nameof(Plan), Plan),
                new Claim(nameof(PlanType), PlanType.ToString()),
                new Claim(nameof(Seats), Seats.ToString()),
                new Claim(nameof(MaxCollections), MaxCollections.ToString()),
                new Claim(nameof(UsePolicies), UsePolicies.ToString()),
                new Claim(nameof(UseSso), UseSso.ToString()),
                new Claim(nameof(UseKeyConnector), UseKeyConnector.ToString()),
                new Claim(nameof(UseScim), UseScim.ToString()),
                new Claim(nameof(UseGroups), UseGroups.ToString()),
                new Claim(nameof(UseEvents), UseEvents.ToString()),
                new Claim(nameof(UseDirectory), UseDirectory.ToString()),
                new Claim(nameof(UseTotp), UseTotp.ToString()),
                new Claim(nameof(Use2fa), Use2fa.ToString()),
                new Claim(nameof(UseApi), UseApi.ToString()),
                new Claim(nameof(UseResetPassword), UseResetPassword.ToString()),
                new Claim(nameof(MaxStorageGb), MaxStorageGb.ToString()),
                new Claim(nameof(SelfHost), SelfHost.ToString()),
                new Claim(nameof(UsersGetPremium), UsersGetPremium.ToString()),
                new Claim(nameof(UseCustomPermissions), UseCustomPermissions.ToString()),
                new Claim(nameof(Version), Version.ToString()),
                new Claim(nameof(Issued), Issued.ToString()),
                new Claim(nameof(Refresh), Refresh.ToString()),
                new Claim(nameof(Expires), Expires.ToString()),
                new Claim(nameof(ExpirationWithoutGracePeriod), ExpirationWithoutGracePeriod.ToString()),
                new Claim(nameof(UsePasswordManager), UsePasswordManager.ToString()),
                new Claim(nameof(UseSecretsManager), UseSecretsManager.ToString()),
                new Claim(nameof(SmSeats), SmSeats.ToString()),
                new Claim(nameof(SmServiceAccounts), SmServiceAccounts.ToString()),
                new Claim(nameof(LimitCollectionCreationDeletion), LimitCollectionCreationDeletion.ToString()),
                new Claim(nameof(AllowAdminAccessToAllCollectionItems), AllowAdminAccessToAllCollectionItems.ToString()),
                new Claim(nameof(Trial), Trial.ToString()),
                new Claim(nameof(LicenseType), LicenseType?.ToString() ?? string.Empty)
            ]),
            Issuer = "Bitwarden",
            Audience = Id.ToString(),
            NotBefore = Issued,
            Expires = Issued.AddDays(7),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateJwtSecurityToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
