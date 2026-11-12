using Bit.Core.Entities;
using Bit.Core.KeyManagement.Repositories;

namespace Bit.Core.KeyManagement.KeyService;

/// <summary>
/// Reads the keys a user owns, sealed by their encryption root.
/// </summary>
public interface IOwnKeysQuery
{
    /// <summary>
    /// Returns <paramref name="user"/>'s private key, signing key and root history, narrowed by
    /// <paramref name="filter"/>.
    /// </summary>
    Task<IReadOnlyList<OwnKeyView>> Run(User user, KeyFilter filter);
}

public class OwnKeysQuery(
    IUserSignatureKeyPairRepository signatureKeyPairRepository,
    IKeyIdReader keyIdReader) : IOwnKeysQuery
{
    public async Task<IReadOnlyList<OwnKeyView>> Run(User user, KeyFilter filter)
    {
        var owner = new KeyOwner(KeyOwnerType.User, user.Id);
        var rootKeyId = user.GetUserKeyId()?.ToString();
        var keys = new List<OwnKeyView>();

        // The key columns keep no creation date per key; the last rotation replaced them all.
        var createdAt = user.LastKeyRotationDate ?? user.CreationDate;

        if (user.PrivateKey is not null && user.PublicKey is not null)
        {
            var keyId = keyIdReader.ReadPublicKeyId(user.PublicKey);
            keys.Add(new(KeyIds.ToResourceId(keyId), KeyResourceTypes.PrivateKey, owner, KeyPurposes.UserSharingRoot,
                keyId, rootKeyId, createdAt, KeyData.PrivateKey(user.PrivateKey, user.PublicKey, user.SignedPublicKey)));
        }

        var signingKey = await SigningKeyAsync(user, owner, rootKeyId, createdAt);
        if (signingKey is not null)
        {
            keys.Add(signingKey);
        }

        // Only the current root is stored, so the history reads back to the first root only while the user
        // has never rotated: then the current root is the first. Without its id, there is no history.
        if (rootKeyId is not null && user.LastKeyRotationDate is null)
        {
            keys.Add(new(user.Id, KeyResourceTypes.SymmetricKey, owner, KeyPurposes.UserEncryptionRoot,
                KeyIds.HistoryMarker, rootKeyId, user.CreationDate, ""));
        }

        return keys.Where(key => filter.Matches(key.KeyPurpose, key.KeyId)).ToList();
    }

    private async Task<OwnKeyView?> SigningKeyAsync(User user, KeyOwner owner, string? rootKeyId, DateTime createdAt)
    {
        // Only V2 users have a signing key.
        if (user.GetSecurityVersion() < SecurityVersions.V2)
        {
            return null;
        }

        var signatureKeyPair = await signatureKeyPairRepository.GetByUserIdAsync(user.Id);
        if (signatureKeyPair is null)
        {
            return null;
        }

        var keyId = keyIdReader.ReadVerifyingKeyId(signatureKeyPair.VerifyingKey);
        return new(KeyIds.ToResourceId(keyId), KeyResourceTypes.SigningKey, owner, KeyPurposes.UserSigningRoot, keyId,
            rootKeyId, createdAt, KeyData.SigningKey(signatureKeyPair.WrappedSigningKey, signatureKeyPair.VerifyingKey));
    }
}
