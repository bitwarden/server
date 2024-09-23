using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Tools.Entities;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.AdminConsole.Entities;

public class Organization : ITableObject<Guid>, IStorableSubscriber, IRevisable, IReferenceable
{
    private Dictionary<TwoFactorProviderType, TwoFactorProvider>? _twoFactorProviders;

    public Guid Id { get; set; }
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
    public string? BusinessName { get; set; }
    [MaxLength(50)]
    public string? BusinessAddress1 { get; set; }
    [MaxLength(50)]
    public string? BusinessAddress2 { get; set; }
    [MaxLength(50)]
    public string? BusinessAddress3 { get; set; }
    [MaxLength(2)]
    public string? BusinessCountry { get; set; }
    [MaxLength(30)]
    public string? BusinessTaxNumber { get; set; }
    [MaxLength(256)]
    public string BillingEmail { get; set; } = null!;
    [MaxLength(50)]
    public string Plan { get; set; } = null!;
    public PlanType PlanType { get; set; }
    public int? Seats { get; set; }
    public short? MaxCollections { get; set; }
    public bool UsePolicies { get; set; }
    public bool UseSso { get; set; }
    public bool UseKeyConnector { get; set; }
    public bool UseScim { get; set; }
    public bool UseGroups { get; set; }
    public bool UseDirectory { get; set; }
    public bool UseEvents { get; set; }
    public bool UseTotp { get; set; }
    public bool Use2fa { get; set; }
    public bool UseApi { get; set; }
    public bool UseResetPassword { get; set; }
    public bool UseSecretsManager { get; set; }
    public bool SelfHost { get; set; }
    public bool UsersGetPremium { get; set; }
    public bool UseCustomPermissions { get; set; }
    public long? Storage { get; set; }
    public short? MaxStorageGb { get; set; }
    public GatewayType? Gateway { get; set; }
    [MaxLength(50)]
    public string? GatewayCustomerId { get; set; }
    [MaxLength(50)]
    public string? GatewaySubscriptionId { get; set; }
    public string? ReferenceData { get; set; }
    public bool Enabled { get; set; } = true;
    [MaxLength(100)]
    public string? LicenseKey { get; set; }
    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
    public string? TwoFactorProviders { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public int? MaxAutoscaleSeats { get; set; } = null;
    public DateTime? OwnersNotifiedOfAutoscaling { get; set; } = null;
    public OrganizationStatusType Status { get; set; }
    public bool UsePasswordManager { get; set; }
    public int? SmSeats { get; set; }
    public int? SmServiceAccounts { get; set; }
    public int? MaxAutoscaleSmSeats { get; set; }
    public int? MaxAutoscaleSmServiceAccounts { get; set; }
    /// <summary>
    /// If set to true, only owners, admins, and some custom users can create and delete collections.
    /// If set to false, any organization member can create a collection, and any member can delete a collection that
    /// they have Can Manage permissions for.
    /// </summary>
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    // Deprecated by https://bitwarden.atlassian.net/browse/PM-10863. This
    // was replaced with `LimitCollectionCreation` and
    // `LimitCollectionDeletion`.
    public bool LimitCollectionCreationDeletion
    {
        get => LimitCollectionCreation || LimitCollectionDeletion;
        set
        {
            LimitCollectionCreation = value;
            LimitCollectionDeletion = value;
        }
    }

    /// <summary>
    /// If set to true, admins, owners, and some custom users can read/write all collections and items in the Admin Console.
    /// If set to false, users generally need collection-level permissions to read/write a collection or its items.
    /// </summary>
    public bool AllowAdminAccessToAllCollectionItems { get; set; }

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
    public string? DisplayBusinessName()
    {
        return WebUtility.HtmlDecode(BusinessName);
    }

    public string? BillingEmailAddress()
    {
        return BillingEmail?.ToLowerInvariant()?.Trim();
    }

    public string? BillingName()
    {
        return DisplayBusinessName();
    }

    public string? SubscriberName()
    {
        return DisplayName();
    }

    public string BraintreeCustomerIdPrefix()
    {
        return "o";
    }

    public string BraintreeIdField()
    {
        return "organization_id";
    }

    public string BraintreeCloudRegionField()
    {
        return "region";
    }

    public string GatewayIdField()
    {
        return "organizationId";
    }

    public bool IsOrganization() => true;

    public bool IsUser()
    {
        return false;
    }

    public string SubscriberType()
    {
        return "Organization";
    }

    public bool IsExpired() => ExpirationDate.HasValue && ExpirationDate.Value <= DateTime.UtcNow;

    public long StorageBytesRemaining()
    {
        if (!MaxStorageGb.HasValue)
        {
            return 0;
        }

        return StorageBytesRemaining(MaxStorageGb.Value);
    }

    public long StorageBytesRemaining(short maxStorageGb)
    {
        var maxStorageBytes = maxStorageGb * 1073741824L;
        if (!Storage.HasValue)
        {
            return maxStorageBytes;
        }

        return maxStorageBytes - Storage.Value;
    }

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

    public bool TwoFactorProviderIsEnabled(TwoFactorProviderType provider)
    {
        var providers = GetTwoFactorProviders();
        if (providers == null || !providers.ContainsKey(provider))
        {
            return false;
        }

        return providers[provider].Enabled && Use2fa;
    }

    public bool TwoFactorIsEnabled()
    {
        var providers = GetTwoFactorProviders();
        if (providers == null)
        {
            return false;
        }

        return providers.Any(p => (p.Value?.Enabled ?? false) && Use2fa);
    }

    public TwoFactorProvider? GetTwoFactorProvider(TwoFactorProviderType provider)
    {
        var providers = GetTwoFactorProviders();
        if (providers == null || !providers.ContainsKey(provider))
        {
            return null;
        }

        return providers[provider];
    }

    public void UpdateFromLicense(OrganizationLicense license)
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
        AllowAdminAccessToAllCollectionItems = license.AllowAdminAccessToAllCollectionItems;
    }
}
