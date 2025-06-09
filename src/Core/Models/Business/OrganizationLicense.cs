using System.Security.Claims;
using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses.Attributes;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.Models.Business;

public class OrganizationLicense : BaseLicense
{
    public OrganizationLicense()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationLicense"/> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ DEPRECATED: This constructor and the entire property-based licensing system is deprecated.
    /// Do not add new properties to this constructor or extend its functionality.
    /// </para>
    /// <para>
    /// This implementation has been replaced by a new claims-based licensing system that provides better security
    /// and flexibility. The new system uses JWT claims to store and validate license information, making it more
    /// secure and easier to extend without requiring changes to the license format.
    /// </para>
    /// <para>
    /// For new license-related features or modifications:
    /// 1. Use the claims-based system instead of adding properties here
    /// 2. Add new claims to the license token
    /// 3. Validate claims in the <see cref="CanUse"/> and <see cref="VerifyData"/> methods
    /// </para>
    /// <para>
    /// This constructor is maintained only for backward compatibility with existing licenses.
    /// </para>
    /// </remarks>
    /// <param name="org">The organization to create the license for.</param>
    /// <param name="subscriptionInfo">Information about the organization's subscription.</param>
    /// <param name="installationId">The ID of the current installation.</param>
    /// <param name="licenseService">The service used to sign the license.</param>
    /// <param name="version">Optional version number for the license format.</param>
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

    private bool ValidLicenseVersion
    {
        get => Version is >= 1 and <= CurrentLicenseFileVersion + 1;
    }

    public override byte[] GetDataBytes(bool forHash = false)
    {
        if (!ValidLicenseVersion)
        {
            throw new NotSupportedException($"Version {Version} is not supported.");
        }

        return this.GetDataBytesWithAttributes(forHash);
    }

    public bool CanUse(
        IGlobalSettings globalSettings,
        ClaimsPrincipal claimsPrincipal,
        out string exception)
    {
        var errorMessages = new StringBuilder();

        var enabled = claimsPrincipal.GetValue<bool>(nameof(Enabled));
        if (!enabled)
        {
            errorMessages.AppendLine("Your cloud-hosted organization is currently disabled.");
        }

        var installationId = claimsPrincipal.GetValue<Guid>(nameof(InstallationId));
        if (installationId != globalSettings.Installation.Id)
        {
            errorMessages.AppendLine("The installation ID does not match the current installation.");
        }

        var selfHost = claimsPrincipal.GetValue<bool>(nameof(SelfHost));
        if (!selfHost)
        {
            errorMessages.AppendLine("The license does not allow for on-premise hosting of organizations.");
        }

        var licenseType = claimsPrincipal.GetValue<LicenseType>(nameof(LicenseType));
        if (licenseType != LicenseType.Organization)
        {
            errorMessages.AppendLine("Premium licenses cannot be applied to an organization. " +
                                     "Upload this license from your personal account settings page.");
        }

        if (errorMessages.Length > 0)
        {
            exception = $"Invalid license. {errorMessages.ToString().TrimEnd()}";
            return false;
        }

        exception = "";
        return true;
    }

    public bool VerifyData(
        Organization organization,
        ClaimsPrincipal claimsPrincipal,
        IGlobalSettings globalSettings)
    {
        var issued = claimsPrincipal.GetValue<DateTime>(nameof(Issued));
        var expires = claimsPrincipal.GetValue<DateTime>(nameof(Expires));
        var installationId = claimsPrincipal.GetValue<Guid>(nameof(InstallationId));
        var licenseKey = claimsPrincipal.GetValue<string>(nameof(LicenseKey));
        var enabled = claimsPrincipal.GetValue<bool>(nameof(Enabled));
        var planType = claimsPrincipal.GetValue<PlanType>(nameof(PlanType));
        var seats = claimsPrincipal.GetValue<int?>(nameof(Seats));
        var maxCollections = claimsPrincipal.GetValue<short?>(nameof(MaxCollections));
        var useGroups = claimsPrincipal.GetValue<bool>(nameof(UseGroups));
        var useDirectory = claimsPrincipal.GetValue<bool>(nameof(UseDirectory));
        var useTotp = claimsPrincipal.GetValue<bool>(nameof(UseTotp));
        var selfHost = claimsPrincipal.GetValue<bool>(nameof(SelfHost));
        var name = claimsPrincipal.GetValue<string>(nameof(Name));
        var usersGetPremium = claimsPrincipal.GetValue<bool>(nameof(UsersGetPremium));
        var useEvents = claimsPrincipal.GetValue<bool>(nameof(UseEvents));
        var use2fa = claimsPrincipal.GetValue<bool>(nameof(Use2fa));
        var useApi = claimsPrincipal.GetValue<bool>(nameof(UseApi));
        var usePolicies = claimsPrincipal.GetValue<bool>(nameof(UsePolicies));
        var useSso = claimsPrincipal.GetValue<bool>(nameof(UseSso));
        var useResetPassword = claimsPrincipal.GetValue<bool>(nameof(UseResetPassword));
        var useKeyConnector = claimsPrincipal.GetValue<bool>(nameof(UseKeyConnector));
        var useScim = claimsPrincipal.GetValue<bool>(nameof(UseScim));
        var useCustomPermissions = claimsPrincipal.GetValue<bool>(nameof(UseCustomPermissions));
        var useSecretsManager = claimsPrincipal.GetValue<bool>(nameof(UseSecretsManager));
        var usePasswordManager = claimsPrincipal.GetValue<bool>(nameof(UsePasswordManager));
        var smSeats = claimsPrincipal.GetValue<int?>(nameof(SmSeats));
        var smServiceAccounts = claimsPrincipal.GetValue<int?>(nameof(SmServiceAccounts));
        var useAdminSponsoredFamilies = claimsPrincipal.GetValue<bool>(nameof(UseAdminSponsoredFamilies));
        var useOrganizationDomains = claimsPrincipal.GetValue<bool>(nameof(UseOrganizationDomains));

        return issued <= DateTime.UtcNow &&
               expires >= DateTime.UtcNow &&
               installationId == globalSettings.Installation.Id &&
               licenseKey == organization.LicenseKey &&
               enabled == organization.Enabled &&
               planType == organization.PlanType &&
               seats == organization.Seats &&
               maxCollections == organization.MaxCollections &&
               useGroups == organization.UseGroups &&
               useDirectory == organization.UseDirectory &&
               useTotp == organization.UseTotp &&
               selfHost == organization.SelfHost &&
               name == organization.Name &&
               usersGetPremium == organization.UsersGetPremium &&
               useEvents == organization.UseEvents &&
               use2fa == organization.Use2fa &&
               useApi == organization.UseApi &&
               usePolicies == organization.UsePolicies &&
               useSso == organization.UseSso &&
               useResetPassword == organization.UseResetPassword &&
               useKeyConnector == organization.UseKeyConnector &&
               useScim == organization.UseScim &&
               useCustomPermissions == organization.UseCustomPermissions &&
               useSecretsManager == organization.UseSecretsManager &&
               usePasswordManager == organization.UsePasswordManager &&
               smSeats == organization.SmSeats &&
               smServiceAccounts == organization.SmServiceAccounts &&
               useAdminSponsoredFamilies == organization.UseAdminSponsoredFamilies &&
               useOrganizationDomains == organization.UseOrganizationDomains;
    }
}
