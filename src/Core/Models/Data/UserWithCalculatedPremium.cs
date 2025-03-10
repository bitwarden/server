using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

/// <summary>
/// Represents a user with an additional property indicating if the user has premium access.
/// </summary>
public class UserWithCalculatedPremium : User
{
    public UserWithCalculatedPremium() { }

    public UserWithCalculatedPremium(User user)
    {
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        EmailVerified = user.EmailVerified;
        MasterPassword = user.MasterPassword;
        MasterPasswordHint = user.MasterPasswordHint;
        Culture = user.Culture;
        SecurityStamp = user.SecurityStamp;
        TwoFactorProviders = user.TwoFactorProviders;
        TwoFactorRecoveryCode = user.TwoFactorRecoveryCode;
        EquivalentDomains = user.EquivalentDomains;
        ExcludedGlobalEquivalentDomains = user.ExcludedGlobalEquivalentDomains;
        AccountRevisionDate = user.AccountRevisionDate;
        Key = user.Key;
        PublicKey = user.PublicKey;
        PrivateKey = user.PrivateKey;
        Premium = user.Premium;
        PremiumExpirationDate = user.PremiumExpirationDate;
        RenewalReminderDate = user.RenewalReminderDate;
        Storage = user.Storage;
        MaxStorageGb = user.MaxStorageGb;
        Gateway = user.Gateway;
        GatewayCustomerId = user.GatewayCustomerId;
        GatewaySubscriptionId = user.GatewaySubscriptionId;
        ReferenceData = user.ReferenceData;
        LicenseKey = user.LicenseKey;
        ApiKey = user.ApiKey;
        Kdf = user.Kdf;
        KdfIterations = user.KdfIterations;
        KdfMemory = user.KdfMemory;
        KdfParallelism = user.KdfParallelism;
        CreationDate = user.CreationDate;
        RevisionDate = user.RevisionDate;
        ForcePasswordReset = user.ForcePasswordReset;
        UsesKeyConnector = user.UsesKeyConnector;
        FailedLoginCount = user.FailedLoginCount;
        LastFailedLoginDate = user.LastFailedLoginDate;
        AvatarColor = user.AvatarColor;
        LastPasswordChangeDate = user.LastPasswordChangeDate;
        LastKdfChangeDate = user.LastKdfChangeDate;
        LastKeyRotationDate = user.LastKeyRotationDate;
        LastEmailChangeDate = user.LastEmailChangeDate;
    }

    /// <summary>
    /// Indicates if the user has premium access, either individually or through an organization.
    /// </summary>
    public bool HasPremiumAccess { get; set; }
}
