using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses.Attributes;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Services;

namespace Bit.Core.Models.Business;

public class OrganizationLicense : BaseLicense
{
    public OrganizationLicense()
    {
    }

    public OrganizationLicense(Organization org, SubscriptionInfo subscriptionInfo, Guid installationId,
        ILicensingService licenseService, int? version = null)
    {
        Version = version.GetValueOrDefault(CurrentLicenseFileVersion); // TODO: Remember to change the constant
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
        UsePasswordManager = org.UsePasswordManager;
        UseSecretsManager = org.UseSecretsManager;
        SmSeats = org.SmSeats;
        SmServiceAccounts = org.SmServiceAccounts;
        UseRiskInsights = org.UseRiskInsights;
        UseOrganizationDomains = org.UseOrganizationDomains;

        // Deprecated. Left for backwards compatibility with old license versions.
        LimitCollectionCreationDeletion = org.LimitCollectionCreation || org.LimitCollectionDeletion;
        AllowAdminAccessToAllCollectionItems = org.AllowAdminAccessToAllCollectionItems;
        //

        Expires = org.CalculateFreshExpirationDate(subscriptionInfo, Issued);
        Refresh = org.CalculateFreshRefreshDate(subscriptionInfo, Expires, Issued);
        ExpirationWithoutGracePeriod = org.CalculateFreshExpirationDateWithoutGracePeriod(subscriptionInfo);
        Trial = org.IsTrialing(subscriptionInfo);

        UseAdminSponsoredFamilies = org.UseAdminSponsoredFamilies;
        Hash = Convert.ToBase64String(this.ComputeHash());
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
    }

    /// <summary>
    /// Represents the current version of the license format. Should be updated whenever new fields are added.
    /// </summary>
    /// <remarks>Intentionally set one version behind to allow self hosted users some time to update before
    /// getting out of date license errors
    /// </remarks>
    public const int CurrentLicenseFileVersion = 15;

    [LicenseVersion(1)]
    public Guid InstallationId { get; set; }

    [LicenseVersion(1)]
    public int? Seats { get; set; }

    [LicenseVersion(1)]
    public short? MaxCollections { get; set; }

    [LicenseVersion(1)]
    public short? MaxStorageGb { get; set; }

    [LicenseVersion(1)]
    public bool Enabled { get; set; }

    [LicenseVersion(1)]
    public bool SelfHost { get; set; }

    [LicenseVersion(1)]
    public bool UseDirectory { get; set; }

    [LicenseVersion(1)]
    public bool UseGroups { get; set; }

    [LicenseVersion(1)]
    public bool UseTotp { get; set; }

    [LicenseVersion(1)]
    public string BillingEmail { get; set; }

    [LicenseVersion(1)]
    public string BusinessName { get; set; }

    [LicenseVersion(1)]
    public string Plan { get; set; }

    [LicenseVersion(1)]
    public PlanType PlanType { get; set; }

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

    // Deprecated. Left for backwards compatibility with old license versions.
    [LicenseVersion(14)]
    public bool LimitCollectionCreationDeletion { get; set; } = true;

    [LicenseVersion(15)]
    public bool AllowAdminAccessToAllCollectionItems { get; set; } = true;
    //

    [LicenseVersion(16)]
    public bool UseOrganizationDomains { get; set; }

    [LicenseIgnore]
    public bool UseAdminSponsoredFamilies { get; set; }

    [LicenseIgnore]
    public bool UseRiskInsights { get; set; }

    [LicenseIgnore]
    [JsonIgnore]
    public override bool ValidLicenseVersion
    {
        get => Version is >= 1 and <= CurrentLicenseFileVersion + 1;
    }




}
