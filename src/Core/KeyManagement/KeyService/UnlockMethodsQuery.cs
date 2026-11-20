using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.KeyService;

/// <summary>
/// Reads a user's ways to reach their encryption root.
/// </summary>
public interface IUnlockMethodsQuery
{
    /// <summary>
    /// Returns <paramref name="user"/>'s unlock methods, only of <paramref name="kinds"/> when given.
    /// </summary>
    Task<IReadOnlyList<UnlockMethodView>> Run(User user, IReadOnlyCollection<string>? kinds);
}

public class UnlockMethodsQuery(
    IDeviceRepository deviceRepository,
    IWebAuthnCredentialRepository webAuthnCredentialRepository,
    IOrganizationUserRepository organizationUserRepository) : IUnlockMethodsQuery
{
    public async Task<IReadOnlyList<UnlockMethodView>> Run(User user, IReadOnlyCollection<string>? kinds)
    {
        // Every method seals the same encryption root.
        var rootKeyId = user.GetUserKeyId()?.ToString();
        var methods = new List<UnlockMethodView>();

        if (Wants(kinds, UnlockMethodKinds.MasterPassword) && user.Key is not null && !user.UsesKeyConnector &&
            user.HasMasterPassword())
        {
            methods.Add(new(user.Id, user.Id, UnlockMethodKinds.MasterPassword, rootKeyId, user.CreationDate,
                KeyData.MasterPassword(user)));
        }

        if (Wants(kinds, UnlockMethodKinds.KeyConnector) && user.Key is not null && user.UsesKeyConnector)
        {
            methods.Add(new(user.Id, user.Id, UnlockMethodKinds.KeyConnector, rootKeyId, user.CreationDate,
                KeyData.KeyConnector(user.Key))
            {
                Organization = await KeyConnectorOrganizationAsync(user.Id),
            });
        }

        if (Wants(kinds, UnlockMethodKinds.Device))
        {
            methods.AddRange(await DevicesAsync(user.Id, rootKeyId));
        }

        if (Wants(kinds, UnlockMethodKinds.Passkey))
        {
            methods.AddRange(await PasskeysAsync(user.Id, rootKeyId));
        }

        return methods;
    }

    private static bool Wants(IReadOnlyCollection<string>? kinds, string kind) => kinds is null || kinds.Contains(kind);

    private async Task<IEnumerable<UnlockMethodView>> DevicesAsync(Guid userId, string? rootKeyId)
    {
        var devices = await deviceRepository.GetManyByUserIdAsync(userId);

        // Only trusted devices hold the encryption root.
        return devices
            .Where(device => HasAll(device.EncryptedUserKey, device.EncryptedPrivateKey, device.EncryptedPublicKey))
            .Select(device => new UnlockMethodView(device.Id, userId, UnlockMethodKinds.Device, rootKeyId,
                device.CreationDate,
                KeyData.KeyPairUnlock(device.EncryptedUserKey!, device.EncryptedPrivateKey!, device.EncryptedPublicKey!))
            {
                Device = device.Id,
            });
    }

    private async Task<IEnumerable<UnlockMethodView>> PasskeysAsync(Guid userId, string? rootKeyId)
    {
        var credentials = await webAuthnCredentialRepository.GetManyByUserIdAsync(userId);

        // Only passkeys with PRF encryption set up hold the encryption root.
        return credentials
            .Where(credential => HasAll(credential.EncryptedUserKey, credential.EncryptedPrivateKey,
                credential.EncryptedPublicKey))
            .Select(credential => new UnlockMethodView(credential.Id, userId, UnlockMethodKinds.Passkey, rootKeyId,
                credential.CreationDate,
                KeyData.KeyPairUnlock(credential.EncryptedUserKey, credential.EncryptedPrivateKey,
                    credential.EncryptedPublicKey))
            {
                Passkey = credential.Id,
            });
    }

    private static bool HasAll(params string?[] values) => values.All(value => !string.IsNullOrEmpty(value));

    /// <summary>
    /// Returns the organization whose Key Connector holds the user's key: the one the user belongs to that
    /// decrypts members through a Key Connector.
    /// </summary>
    private async Task<Guid?> KeyConnectorOrganizationAsync(Guid userId)
    {
        var memberships = await organizationUserRepository.GetManyDetailsByUserAsync(userId);

        return memberships
            .Where(membership => membership.UseKeyConnector && membership.SsoConfig is not null)
            .Where(membership => SsoConfigurationData.Deserialize(membership.SsoConfig!).MemberDecryptionType ==
                                 MemberDecryptionType.KeyConnector)
            .Select(membership => (Guid?)membership.OrganizationId)
            .FirstOrDefault();
    }
}
