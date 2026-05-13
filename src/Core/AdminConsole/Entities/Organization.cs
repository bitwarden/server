using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// An organization is an entity that allows users to share vault items and
/// manage billing, access control, and other enterprise features depending on the plan.
/// </summary>
public class Organization : ITableObject<Guid>, IStorableSubscriber, IRevisable
{
    private Dictionary<TwoFactorProviderType, TwoFactorProvider>? _twoFactorProviders;

    /// <summary>
    /// A unique identifier for the organization.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// A unique, human-readable identifier used to specify the organization during SSO login.
    /// </summary>
    [MaxLength(50)]
    public string? Identifier { get; set; }
    /// <summary>
    /// This value is HTML encoded. For display purposes use the method DisplayName() instead.
    /// </summary>
    [MaxLength(50)]
    public string Name { get; set; } = null!;
    /// <summary>
    /// This value is HTML encoded. For display purposes use the method DisplayBusinessName() instead.
    /// </summary>
    [MaxLength(50)]
    [Obsolete("This property has been deprecated. Use the 'Name' property instead.")]
    public string? BusinessName { get; set; }
    /// <summary>
    /// The first line of the organization's business address.
    /// </summary>
    [MaxLength(50)]
    public string? BusinessAddress1 { get; set; }
    /// <summary>
    /// The second line of the organization's business address.
    /// </summary>
    [MaxLength(50)]
    public string? BusinessAddress2 { get; set; }
    /// <summary>
    /// The third line of the organization's business address.
    /// </summary>
    [MaxLength(50)]
    public string? BusinessAddress3 { get; set; }
    /// <summary>
    /// The two-letter ISO country code of the organization's business address.
    /// </summary>
    [MaxLength(2)]
    public string? BusinessCountry { get; set; }
    /// <summary>
    /// The organization's tax identification number.
    /// </summary>
    [MaxLength(30)]
    public string? BusinessTaxNumber { get; set; }
    /// <summary>
    /// The billing email address for the organization.
    /// </summary>
    [MaxLength(256)]
    public string BillingEmail { get; set; } = null!;
    /// <summary>
    /// The name of the plan the organization is subscribed to.
    /// It is unclear why this is stored and what it is used for - do not use it.
    /// Use the <see cref="PlanType"/> instead.
    /// </summary>
    [MaxLength(50)]
    public string Plan { get; set; } = null!;
    /// <summary>
    /// The type of plan the organization is subscribed to.
    /// </summary>
    public PlanType PlanType { get; set; }
    /// <summary>
    /// The number of user seats included in the organization's subscription. NULL if the plan has no seat limit.
    /// </summary>
    public int? Seats { get; set; }
    /// <summary>
    /// The maximum number of collections the organization can create. NULL if the plan has no collection limit.
    /// </summary>
    public short? MaxCollections { get; set; }
    /// <summary>
    /// If true, the organization has access to the Policies feature.
    /// </summary>
    public bool UsePolicies { get; set; }
    /// <summary>
    /// If true, the organization has access to the SSO (Single Sign-On) feature.
    /// </summary>
    public bool UseSso { get; set; }
    /// <summary>
    /// If true, the organization has access to the Key Connector feature, which allows SSO without master passwords.
    /// </summary>
    public bool UseKeyConnector { get; set; }
    /// <summary>
    /// If true, the organization has access to the SCIM (System for Cross-domain Identity Management) feature.
    /// This is used for automatic user provisioning.
    /// </summary>
    public bool UseScim { get; set; }
    /// <summary>
    /// If true, the organization has access to the Groups feature.
    /// </summary>
    public bool UseGroups { get; set; }
    /// <summary>
    /// If true, the organization can use Directory Connector.
    /// This is a standalone app used for automatic user provisioning.
    /// </summary>
    public bool UseDirectory { get; set; }
    /// <summary>
    /// If true, the organization has access to the event logs feature.
    /// </summary>
    public bool UseEvents { get; set; }
    /// <summary>
    /// If true, the organization has access to the TOTP feature for vault items.
    /// </summary>
    public bool UseTotp { get; set; }
    /// <summary>
    /// If true, the organization has access to organization-level two-factor authentication.
    /// </summary>
    public bool Use2fa { get; set; }
    /// <summary>
    /// If true, the organization has access to the public API.
    /// </summary>
    public bool UseApi { get; set; }
    /// <summary>
    /// If true, the organization has access to the account recovery (admin password reset) feature.
    /// </summary>
    public bool UseResetPassword { get; set; }
    /// <summary>
    /// If true, the organization is subscribed to the Secrets Manager product.
    /// </summary>
    public bool UseSecretsManager { get; set; }
    /// <summary>
    /// If true, the organization can export a license file which is used to create the organization on
    /// a self-hosted instance. It does not indicate whether this organization is self-hosted.
    /// </summary>
    public bool SelfHost { get; set; }
    /// <summary>
    /// If true, all members of the organization are granted premium features.
    /// </summary>
    public bool UsersGetPremium { get; set; }
    /// <summary>
    /// If true, the organization has access to custom user roles with fine-grained permissions.
    /// </summary>
    public bool UseCustomPermissions { get; set; }
    /// <summary>
    /// The number of bytes of file attachment storage the organization has used.
    /// </summary>
    public long? Storage { get; set; }
    /// <summary>
    /// The maximum number of gigabytes of file attachment storage available to the organization.
    /// </summary>
    public short? MaxStorageGb { get; set; }
    /// <summary>
    /// The payment gateway used for billing.
    /// </summary>
    public GatewayType? Gateway { get; set; }
    /// <summary>
    /// The organization's customer ID in the payment gateway.
    /// </summary>
    [MaxLength(50)]
    public string? GatewayCustomerId { get; set; }
    /// <summary>
    /// The organization's subscription ID in the payment gateway.
    /// </summary>
    [MaxLength(50)]
    public string? GatewaySubscriptionId { get; set; }
    /// <summary>
    /// A JSON blob of reference data, e.g. the signup source.
    /// </summary>
    public string? ReferenceData { get; set; }
    /// <summary>
    /// If true, the organization is active. If false, the organization is disabled and access to its
    /// vault and features are restricted.
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// The license key for the organization. Used by self-hosted instances to validate the license.
    /// </summary>
    [MaxLength(100)]
    public string? LicenseKey { get; set; }
    /// <summary>
    /// The organization's asymmetric public key, used to enrol members in account recovery.
    /// </summary>
    public string? PublicKey { get; set; }
    /// <summary>
    /// The organization's asymmetric private key, encrypted with the organization's symmetric key.
    /// </summary>
    public string? PrivateKey { get; set; }
    /// <summary>
    /// A JSON blob of the organization's two-factor authentication provider configurations.
    /// Use <see cref="GetTwoFactorProviders"/> and <see cref="SetTwoFactorProviders"/> to read and write this field.
    /// </summary>
    public string? TwoFactorProviders { get; set; }
    /// <summary>
    /// The date the organization's license expires. NULL if the license does not expire.
    /// </summary>
    public DateTime? ExpirationDate { get; set; }
    /// <summary>
    /// The date the organization was created.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the organization was last updated.
    /// </summary>
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The maximum number of seats the organization can autoscale to. NULL if autoscaling is not limited.
    /// </summary>
    public int? MaxAutoscaleSeats { get; set; } = null;
    /// <summary>
    /// The date the organization's owners were last notified that the organization had autoscaled.
    /// NULL if owners have not been notified.
    /// </summary>
    public DateTime? OwnersNotifiedOfAutoscaling { get; set; } = null;
    /// <summary>
    /// The current status of the organization, representing its lifecycle state.
    /// </summary>
    public OrganizationStatusType Status { get; set; }
    /// <summary>
    /// If true, the organization has access to the Password Manager product.
    /// This is intended for future use if we separate Password Manager from Secrets Manager (and other products).
    /// For now, all organizations and users implicitly have access to Password Manager.
    /// </summary>
    public bool UsePasswordManager { get; set; }
    /// <summary>
    /// The number of Secrets Manager seats included in the organization's subscription.
    /// NULL if the organization does not have access to Secrets Manager.
    /// </summary>
    public int? SmSeats { get; set; }
    /// <summary>
    /// The number of Secrets Manager machine accounts (service accounts) included in the organization's subscription.
    /// NULL if the organization does not have access to Secrets Manager.
    /// </summary>
    public int? SmServiceAccounts { get; set; }
    /// <summary>
    /// The maximum number of Secrets Manager seats the organization can autoscale to.
    /// NULL if autoscaling is not limited.
    /// </summary>
    public int? MaxAutoscaleSmSeats { get; set; }
    /// <summary>
    /// The maximum number of Secrets Manager machine accounts the organization can autoscale to.
    /// NULL if autoscaling is not limited.
    /// </summary>
    public int? MaxAutoscaleSmServiceAccounts { get; set; }
    /// <summary>
    /// If set to true, only owners, admins, and some custom users can create and delete collections.
    /// If set to false, any organization member can create a collection, and any member can delete a collection that
    /// they have Can Manage permissions for.
    /// </summary>
    public bool LimitCollectionCreation { get; set; }
    /// <summary>
    /// If set to true, only owners, admins, and some custom users can delete collections.
    /// If set to false, any member can delete a collection that they have Can Manage permissions for.
    /// </summary>
    public bool LimitCollectionDeletion { get; set; }

    /// <summary>
    /// If set to true, admins, owners, and some custom users can read/write all collections and items in the Admin Console.
    /// If set to false, users generally need collection-level permissions to read/write a collection or its items.
    /// </summary>
    public bool AllowAdminAccessToAllCollectionItems { get; set; }

    /// <summary>
    /// If set to true, members can only delete items when they have a Can Manage permission over the collection.
    /// If set to false, members can delete items when they have a Can Manage OR Can Edit permission over the collection.
    /// </summary>
    public bool LimitItemDeletion { get; set; }

    /// <summary>
    /// If true, the organization can use the Risk Insights feature. This is a reporting feature that provides
    /// insights into the security of an organization.
    /// </summary>
    public bool UseRiskInsights { get; set; }

    /// <summary>
    /// If true, the organization can claim domains, which unlocks additional enterprise features.
    /// </summary>
    public bool UseOrganizationDomains { get; set; }

    /// <summary>
    /// If set to true, admins can initiate organization-issued sponsorships.
    /// </summary>
    public bool UseAdminSponsoredFamilies { get; set; }

    /// <summary>
    /// If set to true, organization needs their seat count synced with their subscription.
    /// </summary>
    public bool SyncSeats { get; set; }

    /// <summary>
    /// If set to true, the organization can use the automatic user confirmation feature.
    /// This automatically confirms users in the Accepted state without requiring manual admin intervention.
    /// There are significant security risks to this and access is manually controlled by our internal teams.
    /// </summary>
    public bool UseAutomaticUserConfirmation { get; set; }

    /// <summary>
    /// If set to true, disables Secrets Manager ads for users in the organization
    /// </summary>
    public bool UseDisableSmAdsForUsers { get; set; }

    /// <summary>
    /// If set to true, the organization can use the phishing blocker feature.
    /// </summary>
    public bool UsePhishingBlocker { get; set; }

    /// <summary>
    /// If set to true, My Items collections will be created automatically when the Organization Data Ownership
    /// policy is enabled.
    /// </summary>
    public bool UseMyItems { get; set; }

    /// <summary>
    /// If set to true, the organization can generate reusable sharable invite links to invite users to the organization.
    /// </summary>
    public bool UseInviteLinks { get; set; }

    /// <summary>
    /// When set to true, the organization is excluded from automated billing
    /// lifecycle operations such as subscription cancellation and disabling for non-payment.
    /// </summary>
    public bool ExemptFromBillingAutomation { get; set; }

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
    public void SetNewId()
    {
        if (Id == default(Guid))
        {
            Id = CoreHelpers.GenerateComb();
        }
    }

    /// <summary>
    /// Returns the name of the organization, HTML decoded ready for display.
    /// </summary>
    public string DisplayName()
    {
        return WebUtility.HtmlDecode(Name);
    }

    /// <summary>
    /// Returns the business name of the organization, HTML decoded ready for display.
    /// </summary>
    ///
    [Obsolete("This method has been deprecated. Use the 'DisplayName()' method instead.")]
    public string? DisplayBusinessName()
    {
        return WebUtility.HtmlDecode(BusinessName);
    }

    /// <inheritdoc/>
    public string? BillingEmailAddress()
    {
        return BillingEmail?.ToLowerInvariant()?.Trim();
    }

    /// <inheritdoc/>
    public string? BillingName()
    {
        return DisplayBusinessName();
    }

    /// <inheritdoc/>
    public string? SubscriberName()
    {
        return DisplayName();
    }

    /// <inheritdoc/>
    public string BraintreeCustomerIdPrefix()
    {
        return "o";
    }

    /// <inheritdoc/>
    public string BraintreeIdField()
    {
        return "organization_id";
    }

    /// <inheritdoc/>
    public string BraintreeCloudRegionField()
    {
        return "region";
    }

    /// <inheritdoc/>
    public string GatewayIdField()
    {
        return "organizationId";
    }

    /// <inheritdoc/>
    public bool IsOrganization() => true;

    /// <inheritdoc/>
    public bool IsUser()
    {
        return false;
    }

    /// <inheritdoc/>
    public string SubscriberType()
    {
        return "Organization";
    }

    /// <summary>
    /// Returns true if the organization's license has expired.
    /// </summary>
    public bool IsExpired() => ExpirationDate.HasValue && ExpirationDate.Value <= DateTime.UtcNow;

    /// <summary>
    /// Returns the number of bytes of file attachment storage remaining for the organization,
    /// based on <see cref="MaxStorageGb"/>. Returns 0 if no storage limit is set.
    /// </summary>
    public long StorageBytesRemaining()
    {
        if (!MaxStorageGb.HasValue)
        {
            return 0;
        }

        return StorageBytesRemaining(MaxStorageGb.Value);
    }

    /// <summary>
    /// Returns the number of bytes of file attachment storage remaining for the organization,
    /// given the specified maximum storage in gigabytes.
    /// </summary>
    public long StorageBytesRemaining(short maxStorageGb)
    {
        var maxStorageBytes = maxStorageGb * 1073741824L;
        if (!Storage.HasValue)
        {
            return maxStorageBytes;
        }

        return maxStorageBytes - Storage.Value;
    }

    /// <summary>
    /// Deserializes <see cref="TwoFactorProviders"/> into a dictionary of two-factor provider configurations.
    /// Returns null if no providers are configured or if the JSON is invalid.
    /// </summary>
    public Dictionary<TwoFactorProviderType, TwoFactorProvider>? GetTwoFactorProviders()
    {
        if (string.IsNullOrWhiteSpace(TwoFactorProviders))
        {
            return null;
        }

        try
        {
            if (_twoFactorProviders == null)
            {
                _twoFactorProviders =
                    JsonHelpers.LegacyDeserialize<Dictionary<TwoFactorProviderType, TwoFactorProvider>>(
                        TwoFactorProviders);
            }

            return _twoFactorProviders;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Serializes the given two-factor provider configurations and stores them in <see cref="TwoFactorProviders"/>.
    /// Clears the field if the dictionary is empty.
    /// </summary>
    public void SetTwoFactorProviders(Dictionary<TwoFactorProviderType, TwoFactorProvider> providers)
    {
        if (!providers.Any())
        {
            TwoFactorProviders = null;
            _twoFactorProviders = null;
            return;
        }

        TwoFactorProviders = JsonHelpers.LegacySerialize(providers, JsonHelpers.LegacyEnumKeyResolver);
        _twoFactorProviders = providers;
    }

    /// <summary>
    /// Returns true if the specified two-factor provider is configured and enabled for the organization.
    /// </summary>
    public bool TwoFactorProviderIsEnabled(TwoFactorProviderType provider)
    {
        var providers = GetTwoFactorProviders();
        if (providers == null || !providers.TryGetValue(provider, out var twoFactorProvider))
        {
            return false;
        }

        return twoFactorProvider.Enabled && Use2fa;
    }

    /// <summary>
    /// Returns true if the organization has any two-factor provider configured and enabled.
    /// </summary>
    public bool TwoFactorIsEnabled()
    {
        var providers = GetTwoFactorProviders();
        if (providers == null)
        {
            return false;
        }

        return providers.Any(p => (p.Value?.Enabled ?? false) && Use2fa);
    }

    /// <summary>
    /// Returns the configuration for the specified two-factor provider, or null if it is not configured.
    /// </summary>
    public TwoFactorProvider? GetTwoFactorProvider(TwoFactorProviderType provider)
    {
        var providers = GetTwoFactorProviders();
        return providers?.GetValueOrDefault(provider);
    }

    /// <summary>
    /// Updates the organization's properties from a self-hosted license file.
    /// </summary>
    public void UpdateFromLicense(OrganizationLicense license, IFeatureService featureService)
    {
        // The following properties are intentionally excluded from being updated:
        // - Id - self-hosted org will have its own unique Guid
        // - MaxStorageGb - not enforced for self-hosted because we're not providing the storage

        Name = license.Name;
        BusinessName = license.BusinessName;
        BillingEmail = license.BillingEmail;
        PlanType = license.PlanType;
        Seats = license.Seats;
        MaxCollections = license.MaxCollections;
        UseGroups = license.UseGroups;
        UseDirectory = license.UseDirectory;
        UseEvents = license.UseEvents;
        UseTotp = license.UseTotp;
        Use2fa = license.Use2fa;
        UseApi = license.UseApi;
        UsePolicies = license.UsePolicies;
        UseMyItems = license.UseMyItems;
        UseInviteLinks = license.UseInviteLinks;
        UseSso = license.UseSso;
        UseKeyConnector = license.UseKeyConnector;
        UseScim = license.UseScim;
        UseResetPassword = license.UseResetPassword;
        SelfHost = license.SelfHost;
        UsersGetPremium = license.UsersGetPremium;
        UseCustomPermissions = license.UseCustomPermissions;
        Plan = license.Plan;
        Enabled = license.Enabled;
        ExpirationDate = license.Expires;
        LicenseKey = license.LicenseKey;
        RevisionDate = DateTime.UtcNow;
        UsePasswordManager = license.UsePasswordManager;
        UseSecretsManager = license.UseSecretsManager;
        SmSeats = license.SmSeats;
        SmServiceAccounts = license.SmServiceAccounts;
        UseRiskInsights = license.UseRiskInsights;
        UseOrganizationDomains = license.UseOrganizationDomains;
        UseAdminSponsoredFamilies = license.UseAdminSponsoredFamilies;
        UseDisableSmAdsForUsers = license.UseDisableSmAdsForUsers;
        UseAutomaticUserConfirmation = license.UseAutomaticUserConfirmation;
        UsePhishingBlocker = license.UsePhishingBlocker;
    }
}
