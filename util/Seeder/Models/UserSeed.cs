using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.RustSDK;

namespace Bit.Seeder.Models;

/// <summary>
/// Input for <see cref="Factories.UserSeeder.Create"/>. Plaintext account details plus optional
/// pre-generated key material.
/// </summary>
internal record UserSeed
{
    /// <summary>
    /// NVARCHAR(256), uniquely indexed. Mangled by the factory only when <see cref="Keys"/> is null;
    /// otherwise the caller owns email/key consistency, since this is also the master-password salt.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// NVARCHAR(50). Defaults to the email local part.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Defaults to true; seeded accounts skip the registration email flow.
    /// </summary>
    public bool EmailVerified { get; init; } = true;

    /// <summary>
    /// Personal premium only — not premium granted through an organization.
    /// Also sets <see cref="User.PremiumExpirationDate"/> one year out.
    /// </summary>
    public bool Premium { get; init; }

    /// <summary>
    /// Attachment allowance in gigabytes. <see cref="User.Storage"/> is never seeded, so consumed
    /// storage reads as null even with attachments.
    /// </summary>
    public short? MaxStorageGb { get; init; }

    /// <summary>
    /// Null falls back to <see cref="Factories.UserSeeder.DefaultPassword"/>.
    /// Ignored when <see cref="Keys"/> is supplied.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// PBKDF2 only; Argon2 cannot be seeded. Defaults to 5,000 for speed — production default is
    /// 600,000. Use that for realistic e2e runs.
    /// </summary>
    public int KdfIterations { get; init; } = 5_000;

    /// <summary>
    /// Spreads bulk callers across the SDK's pre-generated RSA keypair pool.
    /// Ignored when <see cref="Keys"/> is supplied.
    /// </summary>
    public uint PoolIndex { get; init; }

    /// <summary>
    /// Pre-generated key material. When set, the factory skips both key generation and email mangling.
    /// </summary>
    public UserKeys? Keys { get; init; }

    /// <summary>
    /// Null seeds an account with no billing relationship.
    /// </summary>
    public GatewayType? Gateway { get; init; }

    /// <summary>
    /// Stripe <c>cus_…</c>. VARCHAR(50), not validated against Stripe.
    /// </summary>
    public string? GatewayCustomerId { get; init; }

    /// <summary>
    /// Stripe <c>sub_…</c>. VARCHAR(50), not validated against Stripe.
    /// </summary>
    public string? GatewaySubscriptionId { get; init; }

    /// <summary>
    /// NVARCHAR(50). Stored in plaintext and emailed on request — never put the password here.
    /// </summary>
    public string? MasterPasswordHint { get; init; }

    /// <summary>
    /// NVARCHAR(10). Null leaves the entity default ("en-US") in place.
    /// </summary>
    public string? Culture { get; init; }

    /// <summary>
    /// VARCHAR(7) — exactly a <c>#rrggbb</c> hex triplet. Null renders the default avatar.
    /// </summary>
    public string? AvatarColor { get; init; }

    /// <summary>
    /// Requires a new master password before the account is usable. Production sets this through
    /// admin-recovery and TDE-offboarding flows that also stage key material; seeding the flag does not.
    /// </summary>
    public bool ForcePasswordReset { get; init; }

    /// <summary>
    /// Gates emergency-access takeover, self-service email change, and password update. A flag only —
    /// the factory still writes a master password, so this is not a faithful key-connector account.
    /// </summary>
    public bool UsesKeyConnector { get; init; }

    /// <summary>
    /// Serialized via <see cref="User.SetTwoFactorProviders"/>. Provider metadata (TOTP secrets,
    /// WebAuthn credentials) is the caller's to supply.
    /// </summary>
    public Dictionary<TwoFactorProviderType, TwoFactorProvider>? TwoFactorProviders { get; init; }

    /// <summary>
    /// Backdates <see cref="User.CreationDate"/> for aged-account scenarios. Null leaves the entity default
    /// (UtcNow). RevisionDate/AccountRevisionDate are unaffected.
    /// </summary>
    public DateTime? CreationDate { get; init; }
}
