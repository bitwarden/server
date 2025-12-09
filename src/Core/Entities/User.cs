using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Entities;

public class User : ITableObject<Guid>, IStorableSubscriber, IRevisable, ITwoFactorProvidersUser
{
    private Dictionary<TwoFactorProviderType, TwoFactorProvider>? _twoFactorProviders;

    public Guid Id { get; set; }
    [MaxLength(50)]
    public string? Name { get; set; }
    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = null!;
    public bool EmailVerified { get; set; }
    /// <summary>
    /// The server-side master-password hash
    /// </summary>
    [MaxLength(300)]
    public string? MasterPassword { get; set; }
    [MaxLength(50)]
    public string? MasterPasswordHint { get; set; }
    [MaxLength(10)]
    public string Culture { get; set; } = "en-US";
    [Required]
    [MaxLength(50)]
    public string SecurityStamp { get; set; } = null!;
    public string? TwoFactorProviders { get; set; }
    [MaxLength(32)]
    public string? TwoFactorRecoveryCode { get; set; }
    public string? EquivalentDomains { get; set; }
    public string? ExcludedGlobalEquivalentDomains { get; set; }
    /// <summary>
    /// The Account Revision Date is used to check if new sync needs to occur. It should be updated
    /// whenever a change is made that affects a client's sync data; for example, updating their vault or
    /// organization membership.
    /// </summary>
    public DateTime AccountRevisionDate { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The master-password-sealed user key.
    /// </summary>
    public string? Key { get; set; }
    /// <summary>
    /// The raw public key, without a signature from the user's signature key.
    /// </summary>
    public string? PublicKey { get; set; }
    /// <summary>
    /// User key wrapped private key.
    /// </summary>
    public string? PrivateKey { get; set; }
    /// <summary>
    /// The public key, signed by the user's signature key.
    /// </summary>
    public string? SignedPublicKey { get; set; }
    /// <summary>
    /// The security version is included in the security state, but needs COSE parsing
    /// </summary>
    public int? SecurityVersion { get; set; }
    /// <summary>
    /// The security state is a signed object attesting to the version of the user's account.
    /// </summary>
    public string? SecurityState { get; set; }
    public bool Premium { get; set; }
    public DateTime? PremiumExpirationDate { get; set; }
    public DateTime? RenewalReminderDate { get; set; }
    public long? Storage { get; set; }
    public short? MaxStorageGb { get; set; }
    public GatewayType? Gateway { get; set; }
    [MaxLength(50)]
    public string? GatewayCustomerId { get; set; }
    [MaxLength(50)]
    public string? GatewaySubscriptionId { get; set; }
    public string? ReferenceData { get; set; }
    [MaxLength(100)]
    public string? LicenseKey { get; set; }
    [Required]
    [MaxLength(30)]
    public string ApiKey { get; set; } = null!;
    public KdfType Kdf { get; set; } = KdfType.PBKDF2_SHA256;
    public int KdfIterations { get; set; } = AuthConstants.PBKDF2_ITERATIONS.Default;
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public bool ForcePasswordReset { get; set; }
    public bool UsesKeyConnector { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LastFailedLoginDate { get; set; }
    [MaxLength(7)]
    public string? AvatarColor { get; set; }
    public DateTime? LastPasswordChangeDate { get; set; }
    public DateTime? LastKdfChangeDate { get; set; }
    public DateTime? LastKeyRotationDate { get; set; }
    public DateTime? LastEmailChangeDate { get; set; }
    public bool VerifyDevices { get; set; } = true;
    // PM-28827 Uncomment below line.
    // public string? MasterPasswordSalt { get; set; }

    public string GetMasterPasswordSalt()
    {
        return Email.ToLowerInvariant().Trim();
    }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public string? BillingEmailAddress()
    {
        return Email?.ToLowerInvariant()?.Trim();
    }

    public string? BillingName()
    {
        return Name;
    }

    public string SubscriberName()
    {
        return string.IsNullOrWhiteSpace(Name) ? Email : Name;
    }

    public string BraintreeCustomerIdPrefix()
    {
        return "u";
    }

    public string BraintreeIdField()
    {
        return "user_id";
    }

    public string BraintreeCloudRegionField()
    {
        return "region";
    }

    public string GatewayIdField()
    {
        return "userId";
    }

    public bool IsOrganization() => false;

    public bool IsUser()
    {
        return true;
    }

    public string SubscriberType()
    {
        return "Subscriber";
    }

    public bool IsExpired() => PremiumExpirationDate.HasValue && PremiumExpirationDate.Value <= DateTime.UtcNow;

    /// <summary>
    /// Deserializes the User.TwoFactorProviders property from JSON to the appropriate C# dictionary.
    /// </summary>
    /// <returns>Dictionary of TwoFactor providers</returns>
    public Dictionary<TwoFactorProviderType, TwoFactorProvider>? GetTwoFactorProviders()
    {
        if (string.IsNullOrWhiteSpace(TwoFactorProviders))
        {
            return null;
        }

        try
        {
            _twoFactorProviders ??=
                JsonHelpers.LegacyDeserialize<Dictionary<TwoFactorProviderType, TwoFactorProvider>>(
                    TwoFactorProviders);

            /*
                U2F is no longer supported, and all users keys should have been migrated to WebAuthn.
                To prevent issues with accounts being prompted for unsupported U2F we remove them.
                This will probably exist in perpetuity since there is no way to know for sure if any
                given user does or doesn't have this enabled. It is a non-zero chance.
            */
            _twoFactorProviders?.Remove(TwoFactorProviderType.U2f);

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

    public int GetSecurityVersion()
    {
        // If no security version is set, it is version 1. The minimum initialized version is 2.
        return SecurityVersion ?? 1;
    }

    /// <summary>
    /// Serializes the C# object to the User.TwoFactorProviders property in JSON format.
    /// </summary>
    /// <param name="providers">Dictionary of Two Factor providers</param>
    public void SetTwoFactorProviders(Dictionary<TwoFactorProviderType, TwoFactorProvider> providers)
    {
        // When replacing with system.text remember to remove the extra serialization in WebAuthnTokenProvider.
        TwoFactorProviders = JsonHelpers.LegacySerialize(providers, JsonHelpers.LegacyEnumKeyResolver);
        _twoFactorProviders = providers;
    }

    /// <summary>
    /// Checks if the user has a specific TwoFactorProvider configured. If a user has a premium TwoFactor
    /// configured it will still be found, even if the user's premium subscription has ended.
    /// </summary>
    /// <param name="provider">TwoFactor provider being searched for</param>
    /// <returns>TwoFactorProvider if found; null otherwise.</returns>
    public TwoFactorProvider? GetTwoFactorProvider(TwoFactorProviderType provider)
    {
        var providers = GetTwoFactorProviders();
        return providers?.GetValueOrDefault(provider);
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

    public bool HasMasterPassword()
    {
        return MasterPassword != null;
    }

    public PublicKeyEncryptionKeyPairData GetPublicKeyEncryptionKeyPair()
    {
        if (string.IsNullOrWhiteSpace(PrivateKey) || string.IsNullOrWhiteSpace(PublicKey))
        {
            throw new InvalidOperationException("User public key encryption key pair is not fully initialized.");
        }

        return new PublicKeyEncryptionKeyPairData(PrivateKey, PublicKey, SignedPublicKey);
    }
}
