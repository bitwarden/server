using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Entities;

public class User : ITableObject<Guid>, ISubscriber, IStorable, IStorableSubscriber, IRevisable, ITwoFactorProvidersUser, IReferenceable
{
    private Dictionary<TwoFactorProviderType, TwoFactorProvider> _twoFactorProviders;

    public Guid Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [Required]
    [MaxLength(256)]
    public string Email { get; set; }
    public bool EmailVerified { get; set; }
    [MaxLength(300)]
    public string MasterPassword { get; set; }
    [MaxLength(50)]
    public string MasterPasswordHint { get; set; }
    [MaxLength(10)]
    public string Culture { get; set; } = "en-US";
    [Required]
    [MaxLength(50)]
    public string SecurityStamp { get; set; }
    public string TwoFactorProviders { get; set; }
    [MaxLength(32)]
    public string TwoFactorRecoveryCode { get; set; }
    public string EquivalentDomains { get; set; }
    public string ExcludedGlobalEquivalentDomains { get; set; }
    public DateTime AccountRevisionDate { get; set; } = DateTime.UtcNow;
    public string Key { get; set; }
    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }
    public bool Premium { get; set; }
    public DateTime? PremiumExpirationDate { get; set; }
    public DateTime? RenewalReminderDate { get; set; }
    public long? Storage { get; set; }
    public short? MaxStorageGb { get; set; }
    public GatewayType? Gateway { get; set; }
    [MaxLength(50)]
    public string GatewayCustomerId { get; set; }
    [MaxLength(50)]
    public string GatewaySubscriptionId { get; set; }
    public string ReferenceData { get; set; }
    [MaxLength(100)]
    public string LicenseKey { get; set; }
    [Required]
    [MaxLength(30)]
    public string ApiKey { get; set; }
    public KdfType Kdf { get; set; } = KdfType.PBKDF2_SHA256;
    public int KdfIterations { get; set; } = 5000;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public bool ForcePasswordReset { get; set; }
    public bool UsesKeyConnector { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LastFailedLoginDate { get; set; }
    public bool UnknownDeviceVerificationEnabled { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public string BillingEmailAddress()
    {
        return Email?.ToLowerInvariant()?.Trim();
    }

    public string BillingName()
    {
        return Name;
    }

    public string BraintreeCustomerIdPrefix()
    {
        return "u";
    }

    public string BraintreeIdField()
    {
        return "user_id";
    }

    public string GatewayIdField()
    {
        return "userId";
    }

    public bool IsUser()
    {
        return true;
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

    public Guid? GetUserId()
    {
        return Id;
    }

    public bool GetPremium()
    {
        return Premium;
    }

    public void SetTwoFactorProviders(Dictionary<TwoFactorProviderType, TwoFactorProvider> providers)
    {
        // When replacing with system.text remember to remove the extra serialization in WebAuthnTokenProvider.
        TwoFactorProviders = JsonHelpers.LegacySerialize(providers, JsonHelpers.LegacyEnumKeyResolver);
        _twoFactorProviders = providers;
    }

    public void ClearTwoFactorProviders()
    {
        SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>());
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

    public IdentityUser ToIdentityUser(bool twoFactorEnabled)
    {
        return new IdentityUser
        {
            Id = Id.ToString(),
            Email = Email,
            NormalizedEmail = Email,
            EmailConfirmed = EmailVerified,
            UserName = Email,
            NormalizedUserName = Email,
            TwoFactorEnabled = twoFactorEnabled,
            SecurityStamp = SecurityStamp
        };
    }
}
