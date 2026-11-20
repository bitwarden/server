using Bit.Core.Entities;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.KeyService;

/// <summary>
/// Reads the public keys of the named owners.
/// </summary>
public interface IPublicKeysQuery
{
    /// <summary>
    /// Returns the public keys of <paramref name="ownerIds"/>, all of <paramref name="ownerType"/>, narrowed by
    /// <paramref name="filter"/>.
    /// </summary>
    Task<IReadOnlyList<PublicKeyView>> Run(KeyOwnerType ownerType, IReadOnlyCollection<Guid> ownerIds, KeyFilter filter);
}

public class PublicKeysQuery(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IUserSignatureKeyPairRepository signatureKeyPairRepository,
    IKeyIdReader keyIdReader) : IPublicKeysQuery
{
    public async Task<IReadOnlyList<PublicKeyView>> Run(
        KeyOwnerType ownerType, IReadOnlyCollection<Guid> ownerIds, KeyFilter filter)
    {
        // Providers hold no keys in this server.
        var keys = ownerType switch
        {
            KeyOwnerType.User => await UserKeysAsync(ownerIds),
            KeyOwnerType.Organization => await OrganizationKeysAsync(ownerIds),
            _ => [],
        };

        return keys.Where(key => filter.Matches(key.KeyPurpose, key.KeyId)).ToList();
    }

    private async Task<List<PublicKeyView>> UserKeysAsync(IReadOnlyCollection<Guid> userIds)
    {
        var keys = new List<PublicKeyView>();

        foreach (var user in await userRepository.GetManyAsync(userIds))
        {
            keys.AddRange(await KeysOfAsync(user));
        }

        return keys;
    }

    private async Task<List<PublicKeyView>> KeysOfAsync(User user)
    {
        if (user.PublicKey is null)
        {
            return [];
        }

        // The key columns keep no creation date per key; the last rotation replaced them all.
        var owner = new KeyOwner(KeyOwnerType.User, user.Id);
        var createdAt = user.LastKeyRotationDate ?? user.CreationDate;

        var publicKeyId = keyIdReader.ReadPublicKeyId(user.PublicKey);
        List<PublicKeyView> keys =
        [
            new(KeyIds.ToResourceId(publicKeyId), owner, KeyPurposes.UserSharingRoot, publicKeyId, createdAt,
                KeyData.PublicKey(user.PublicKey, user.SignedPublicKey)),
        ];

        // Only V2 users have a signing key.
        if (user.GetSecurityVersion() < SecurityVersions.V2)
        {
            return keys;
        }

        var signatureKeyPair = await signatureKeyPairRepository.GetByUserIdAsync(user.Id);
        if (signatureKeyPair is null)
        {
            return keys;
        }

        var verifyingKeyId = keyIdReader.ReadVerifyingKeyId(signatureKeyPair.VerifyingKey);
        keys.Add(new(KeyIds.ToResourceId(verifyingKeyId), owner, KeyPurposes.UserSigningRoot, verifyingKeyId,
            createdAt, KeyData.VerifyingKey(signatureKeyPair.VerifyingKey)));

        return keys;
    }

    private async Task<List<PublicKeyView>> OrganizationKeysAsync(IReadOnlyCollection<Guid> organizationIds)
    {
        var keys = new List<PublicKeyView>();

        foreach (var organization in await organizationRepository.GetManyByIdsAsync(organizationIds))
        {
            if (organization.PublicKey is null)
            {
                continue;
            }

            var keyId = keyIdReader.ReadPublicKeyId(organization.PublicKey);
            keys.Add(new(KeyIds.ToResourceId(keyId), new KeyOwner(KeyOwnerType.Organization, organization.Id),
                KeyPurposes.OrganizationSharingRoot, keyId, organization.CreationDate,
                KeyData.PublicKey(organization.PublicKey, null)));
        }

        return keys;
    }
}
