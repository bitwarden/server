namespace Bit.Core.KeyManagement.KeyService;

/// <summary>
/// The public half of a key pair.
/// </summary>
public record PublicKeyView(
    Guid Id,
    KeyOwner Owner,
    string KeyPurpose,
    string KeyId,
    DateTime CreatedAt,
    string Data);

/// <summary>
/// A key its owner reads: a private key, a signing key, or a symmetric key of the root history.
/// </summary>
public record OwnKeyView(
    Guid Id,
    string Type,
    KeyOwner Owner,
    string KeyPurpose,
    string KeyId,
    string? SealedByKeyId,
    DateTime CreatedAt,
    string Data);

/// <summary>
/// A way for a user to reach their encryption root.
/// </summary>
public record UnlockMethodView(
    Guid Id,
    Guid User,
    string Kind,
    string? ContainedKeyId,
    DateTime CreatedAt,
    string Data)
{
    /// <summary>The device, for a <see cref="UnlockMethodKinds.Device"/> method.</summary>
    public Guid? Device { get; init; }

    /// <summary>The passkey, for a <see cref="UnlockMethodKinds.Passkey"/> method.</summary>
    public Guid? Passkey { get; init; }

    /// <summary>The organization, for a <see cref="UnlockMethodKinds.KeyConnector"/> method.</summary>
    public Guid? Organization { get; init; }
}

/// <summary>
/// Narrows a list of keys. A <c>null</c> collection matches everything.
/// </summary>
public record KeyFilter(IReadOnlyCollection<string>? KeyPurposes, IReadOnlyCollection<string>? KeyIds)
{
    public bool Matches(string keyPurpose, string keyId) =>
        (KeyPurposes is null || KeyPurposes.Contains(keyPurpose)) &&
        (KeyIds is null || KeyIds.Contains(keyId));
}
