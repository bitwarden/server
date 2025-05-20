using System.Reflection;
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
                Expires = subscriptionInfo.Subscription.PeriodEndDate?.AddDays(Constants
                    .OrganizationSelfHostSubscriptionGracePeriodDays);
                ExpirationWithoutGracePeriod = subscriptionInfo.Subscription.PeriodEndDate;
            }
            else
            {
                Expires = org.ExpirationDate.HasValue ? org.ExpirationDate.Value.AddMonths(11) : Issued.AddYears(1);
                Refresh = DateTime.UtcNow - Expires > TimeSpan.FromDays(30) ? DateTime.UtcNow.AddDays(30) : Expires;
            }

            Trial = false;
        }

        UseAdminSponsoredFamilies = org.UseAdminSponsoredFamilies;
        Hash = Convert.ToBase64String(ComputeHash());
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
    }

    [LicenseVersion(1)]
    public Guid InstallationId { get; set; }

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

    [LicenseVersion(6)]
    public bool UsePolicies { get; set; }

    [LicenseVersion(7)]
    public bool UseSso { get; set; }

    [LicenseVersion(9)]
    public bool UseKeyConnector { get; set; }

    [LicenseVersion(10)]
    public bool UseScim { get; set; }

    [LicenseVersion(1)]
    public bool UseGroups { get; set; }

    [LicenseVersion(3)]
    public bool UseEvents { get; set; }

    [LicenseVersion(1)]
    public bool UseDirectory { get; set; }

    [LicenseVersion(1)]
    public bool UseTotp { get; set; }

    [LicenseVersion(4)]
    public bool Use2fa { get; set; }

    [LicenseVersion(5)]
    public bool UseApi { get; set; }

    [LicenseVersion(8)]
    public bool UseResetPassword { get; set; }

    [LicenseVersion(1)]
    public short? MaxStorageGb { get; set; }

    [LicenseVersion(1)]
    public bool SelfHost { get; set; }

    [LicenseVersion(2)]
    public bool UsersGetPremium { get; set; }

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

    [LicenseIgnore]
    public bool UseRiskInsights { get; set; }

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

    /// <summary>
    /// Represents the current version of the license format. Should be updated whenever new fields are added.
    /// </summary>
    /// <remarks>Intentionally set one version behind to allow self hosted users some time to update before
    /// getting out of date license errors
    /// </remarks>
    public const int CurrentLicenseFileVersion = 15;

    private bool ValidLicenseVersion
    {
        get => Version is >= 1 and <= 16;
    }

    public override byte[] GetDataBytes(bool forHash = false)
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
                    !p.Name.Equals(nameof(Token)) &&
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
                    // ExpirationWithoutGracePeriod was added in Version 12
                    (Version >= 12 || !p.Name.Equals(nameof(ExpirationWithoutGracePeriod))) &&
                    // UseSecretsManager, UsePasswordManager, SmSeats, and SmServiceAccounts were added in Version 13
                    (Version >= 13 || !p.Name.Equals(nameof(UseSecretsManager))) &&
                    (Version >= 13 || !p.Name.Equals(nameof(UsePasswordManager))) &&
                    (Version >= 13 || !p.Name.Equals(nameof(SmSeats))) &&
                    (Version >= 13 || !p.Name.Equals(nameof(SmServiceAccounts))) &&
                    // LimitCollectionCreationDeletion was added in Version 14
                    (Version >= 14 || !p.Name.Equals(nameof(LimitCollectionCreationDeletion))) &&
                    // AllowAdminAccessToAllCollectionItems was added in Version 15
                    (Version >= 15 || !p.Name.Equals(nameof(AllowAdminAccessToAllCollectionItems))) &&
                    // UseOrganizationDomains was added in Version 16
                    (Version >= 16 || !p.Name.Equals(nameof(UseOrganizationDomains))) &&
                    (
                        !forHash ||
                        (
                            !p.Name.Equals(nameof(Hash)) &&
                            !p.Name.Equals(nameof(Issued)) &&
                            !p.Name.Equals(nameof(Refresh))
                        )
                    ) &&
                    // any new fields added need to be added here so that they're ignored
                    !p.Name.Equals(nameof(UseRiskInsights)) &&
                    !p.Name.Equals(nameof(UseAdminSponsoredFamilies)) &&
                    !p.Name.Equals(nameof(UseOrganizationDomains)))
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

    public bool CanUse(
        IGlobalSettings globalSettings,
        ILicensingService licensingService,
        ClaimsPrincipal claimsPrincipal,
        out string exception)
    {
        if (string.IsNullOrWhiteSpace(Token) || claimsPrincipal is null)
        {
            return ObsoleteCanUse(globalSettings, licensingService, out exception);
        }

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
        if (licenseType != Enums.LicenseType.Organization)
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

    /// <summary>
    /// Validates an obsolete license format using property-based validation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ DEPRECATED: This method is deprecated and should not be extended or modified.
    /// It is maintained only for backward compatibility with old license formats.
    /// </para>
    /// <para>
    /// This method has been replaced by a new claims-based validation system that provides:
    /// - Better security through JWT claims
    /// - More flexible validation rules
    /// - Easier extensibility without changing the license format
    /// - Better separation of concerns
    /// </para>
    /// <para>
    /// To add new license validation rules:
    /// 1. Add new claims to the license token in the claims-based system
    /// 2. Extend the <see cref="CanUse(IGlobalSettings, ILicensingService, ClaimsPrincipal, out string)"/> method
    /// 3. Validate the new claims using the ClaimsPrincipal parameter
    /// </para>
    /// <para>
    /// This method will be removed in a future version once all old licenses have been migrated
    /// to the new claims-based system.
    /// </para>
    /// </remarks>
    /// <param name="globalSettings">The global settings containing installation information.</param>
    /// <param name="licensingService">The service used to verify the license signature.</param>
    /// <param name="exception">When the method returns false, contains the error message explaining why the license is invalid.</param>
    /// <returns>True if the license is valid, false otherwise.</returns>
    private bool ObsoleteCanUse(IGlobalSettings globalSettings, ILicensingService licensingService, out string exception)
    {
        // Do not extend this method. It is only here for backwards compatibility with old licenses.
        var errorMessages = new StringBuilder();

        if (!Enabled)
        {
            errorMessages.AppendLine("Your cloud-hosted organization is currently disabled.");
        }

        if (Issued > DateTime.UtcNow)
        {
            errorMessages.AppendLine("The license hasn't been issued yet.");
        }

        if (Expires < DateTime.UtcNow)
        {
            errorMessages.AppendLine("The license has expired.");
        }

        if (!ValidLicenseVersion)
        {
            errorMessages.AppendLine($"Version {Version} is not supported.");
        }

        if (InstallationId != globalSettings.Installation.Id)
        {
            errorMessages.AppendLine("The installation ID does not match the current installation.");
        }

        if (!SelfHost)
        {
            errorMessages.AppendLine("The license does not allow for on-premise hosting of organizations.");
        }

        if (LicenseType != null && LicenseType != Enums.LicenseType.Organization)
        {
            errorMessages.AppendLine("Premium licenses cannot be applied to an organization. " +
                                     "Upload this license from your personal account settings page.");
        }

        if (!licensingService.VerifyLicense(this))
        {
            errorMessages.AppendLine("The license verification failed.");
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
        if (string.IsNullOrWhiteSpace(Token))
        {
            return ObsoleteVerifyData(organization, globalSettings);
        }

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

    /// <summary>
    /// Do not extend this method. It is only here for backwards compatibility with old licenses.
    /// Instead, extend the VerifyData method using the ClaimsPrincipal.
    /// </summary>
    /// <param name="organization"></param>
    /// <param name="globalSettings"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private bool ObsoleteVerifyData(Organization organization, IGlobalSettings globalSettings)
    {
        // Do not extend this method. It is only here for backwards compatibility with old licenses.
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

        /*Version 12 added ExpirationWithoutDatePeriod, but that property is informational only and is not saved
            to the Organization object. It's validated as part of the hash but does not need to be validated here.
            */

        if (valid && Version >= 13)
        {
            valid = organization.UseSecretsManager == UseSecretsManager &&
                    organization.UsePasswordManager == UsePasswordManager &&
                    organization.SmSeats == SmSeats &&
                    organization.SmServiceAccounts == SmServiceAccounts;
        }

        /*
            * Version 14 added LimitCollectionCreationDeletion and Version
            * 15 added AllowAdminAccessToAllCollectionItems, however they
            * are no longer used and are intentionally excluded from
            * validation.
            */

        if (valid && Version >= 16)
        {
            valid = organization.UseOrganizationDomains;
        }

        return valid;
    }
}
