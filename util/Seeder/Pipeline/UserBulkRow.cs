using Bit.Core.Entities;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Seeder-internal projection of <c>dbo.User</c> with primitive column types and no EF value converters,
/// so <c>Key</c> and <c>MasterPassword</c> are written as-is.
/// Property names match DB column names exactly so LinqToDB maps them by convention.
/// Keep in sync with the <c>dbo.User</c> schema.
/// </summary>
internal sealed class UserBulkRow
{
    public Guid Id { get; init; }

    public string? Name { get; init; }

    public required string Email { get; init; }

    public bool EmailVerified { get; init; }

    public string? MasterPassword { get; init; }

    public string? MasterPasswordHint { get; init; }

    public required string Culture { get; init; }

    public required string SecurityStamp { get; init; }

    public string? TwoFactorProviders { get; init; }

    public string? TwoFactorRecoveryCode { get; init; }

    public string? EquivalentDomains { get; init; }

    public string? ExcludedGlobalEquivalentDomains { get; init; }

    public DateTime AccountRevisionDate { get; init; }

    public string? Key { get; init; }

    public string? PublicKey { get; init; }

    public string? PrivateKey { get; init; }

    public string? SignedPublicKey { get; init; }

    public int? SecurityVersion { get; init; }

    public string? SecurityState { get; init; }

    public bool Premium { get; init; }

    public DateTime? PremiumExpirationDate { get; init; }

    public DateTime? RenewalReminderDate { get; init; }

    public long? Storage { get; init; }

    public short? MaxStorageGb { get; init; }

    public byte? Gateway { get; init; }

    public string? GatewayCustomerId { get; init; }

    public string? GatewaySubscriptionId { get; init; }

    public string? ReferenceData { get; init; }

    public string? LicenseKey { get; init; }

    public required string ApiKey { get; init; }

    public byte Kdf { get; init; }

    public int KdfIterations { get; init; }

    public int? KdfMemory { get; init; }

    public int? KdfParallelism { get; init; }

    public DateTime CreationDate { get; init; }

    public DateTime RevisionDate { get; init; }

    public bool ForcePasswordReset { get; init; }

    public bool UsesKeyConnector { get; init; }

    public int FailedLoginCount { get; init; }

    public DateTime? LastFailedLoginDate { get; init; }

    public string? AvatarColor { get; init; }

    public DateTime? LastPasswordChangeDate { get; init; }

    public DateTime? LastKdfChangeDate { get; init; }

    public DateTime? LastKeyRotationDate { get; init; }

    public DateTime? LastEmailChangeDate { get; init; }

    public bool VerifyDevices { get; init; }

    public string? V2UpgradeToken { get; init; }

    public string? MasterPasswordSalt { get; init; }

    public DateTime? LastApiKeyRotationDate { get; init; }

    public string? UserKeyId { get; init; }

    /// <summary>
    /// Projects a Core <see cref="User"/> onto its persisted columns.
    /// </summary>
    internal static UserBulkRow From(User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        Email = u.Email,
        EmailVerified = u.EmailVerified,
        MasterPassword = u.MasterPassword,
        MasterPasswordHint = u.MasterPasswordHint,
        Culture = u.Culture,
        SecurityStamp = u.SecurityStamp,
        TwoFactorProviders = u.TwoFactorProviders,
        TwoFactorRecoveryCode = u.TwoFactorRecoveryCode,
        EquivalentDomains = u.EquivalentDomains,
        ExcludedGlobalEquivalentDomains = u.ExcludedGlobalEquivalentDomains,
        AccountRevisionDate = u.AccountRevisionDate,
        Key = u.Key,
        PublicKey = u.PublicKey,
        PrivateKey = u.PrivateKey,
        SignedPublicKey = u.SignedPublicKey,
        SecurityVersion = u.SecurityVersion,
        SecurityState = u.SecurityState,
        Premium = u.Premium,
        PremiumExpirationDate = u.PremiumExpirationDate,
        RenewalReminderDate = u.RenewalReminderDate,
        Storage = u.Storage,
        MaxStorageGb = u.MaxStorageGb,
        Gateway = u.Gateway.HasValue ? (byte)u.Gateway.Value : null,
        GatewayCustomerId = u.GatewayCustomerId,
        GatewaySubscriptionId = u.GatewaySubscriptionId,
        ReferenceData = u.ReferenceData,
        LicenseKey = u.LicenseKey,
        ApiKey = u.ApiKey,
        Kdf = (byte)u.Kdf,
        KdfIterations = u.KdfIterations,
        KdfMemory = u.KdfMemory,
        KdfParallelism = u.KdfParallelism,
        CreationDate = u.CreationDate,
        RevisionDate = u.RevisionDate,
        ForcePasswordReset = u.ForcePasswordReset,
        UsesKeyConnector = u.UsesKeyConnector,
        FailedLoginCount = u.FailedLoginCount,
        LastFailedLoginDate = u.LastFailedLoginDate,
        AvatarColor = u.AvatarColor,
        LastPasswordChangeDate = u.LastPasswordChangeDate,
        LastKdfChangeDate = u.LastKdfChangeDate,
        LastKeyRotationDate = u.LastKeyRotationDate,
        LastEmailChangeDate = u.LastEmailChangeDate,
        VerifyDevices = u.VerifyDevices,
        V2UpgradeToken = u.V2UpgradeToken,
        MasterPasswordSalt = u.MasterPasswordSalt,
        LastApiKeyRotationDate = u.LastApiKeyRotationDate,
        UserKeyId = u.UserKeyId,
    };
}
