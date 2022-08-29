using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class Organization : ITableObject<Guid>, ISubscriber, IStorable, IStorableSubscriber, IRevisable, IReferenceable
{
    private Dictionary<TwoFactorProviderType, TwoFactorProvider> _twoFactorProviders;

    public Guid Id { get; set; }
    [MaxLength(50)]
    public string Identifier { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(50)]
    public string BusinessName { get; set; }
    [MaxLength(50)]
    public string BusinessAddress1 { get; set; }
    [MaxLength(50)]
    public string BusinessAddress2 { get; set; }
    [MaxLength(50)]
    public string BusinessAddress3 { get; set; }
    [MaxLength(2)]
    public string BusinessCountry { get; set; }
    [MaxLength(30)]
    public string BusinessTaxNumber { get; set; }
    [MaxLength(256)]
    public string BillingEmail { get; set; }
    [MaxLength(50)]
    public string Plan { get; set; }
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
    public bool SelfHost { get; set; }
    public bool UsersGetPremium { get; set; }
    public long? Storage { get; set; }
    public short? MaxStorageGb { get; set; }
    public GatewayType? Gateway { get; set; }
    [MaxLength(50)]
    public string GatewayCustomerId { get; set; }
    [MaxLength(50)]
    public string GatewaySubscriptionId { get; set; }
    public string ReferenceData { get; set; }
    public bool Enabled { get; set; } = true;
    [MaxLength(100)]
    public string LicenseKey { get; set; }
    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }
    public string TwoFactorProviders { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public int? MaxAutoscaleSeats { get; set; } = null;
    public DateTime? OwnersNotifiedOfAutoscaling { get; set; } = null;

    public void SetNewId()
    {
        if (Id == default(Guid))
        {
            Id = CoreHelpers.GenerateComb();
        }
    }

    public string BillingEmailAddress()
    {
        return BillingEmail?.ToLowerInvariant()?.Trim();
    }

    public string BillingName()
    {
        return BusinessName;
    }

    public string BraintreeCustomerIdPrefix()
    {
        return "o";
    }

    public string BraintreeIdField()
    {
        return "organization_id";
    }

    public string GatewayIdField()
    {
        return "organizationId";
    }

    public bool IsUser()
    {
        return false;
    }

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

    public Dictionary<TwoFactorProviderType, TwoFactorProvider> GetTwoFactorProviders()
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

    public TwoFactorProvider GetTwoFactorProvider(TwoFactorProviderType provider)
    {
        var providers = GetTwoFactorProviders();
        if (providers == null || !providers.ContainsKey(provider))
        {
            return null;
        }

        return providers[provider];
    }
}
