using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.RustSDK;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Seeder.Factories;

internal static class ProviderUserSeeder
{
    /// <summary>
    /// Creates a ProviderUser linking the user to the provider with the requested role and status.
    /// Mirrors <see cref="OrganizationExtensions.CreateOrganizationUserWithKey"/>: the user is linked by
    /// <see cref="ProviderUser.UserId"/> once the status is past <see cref="ProviderUserStatusType.Invited"/>
    /// (otherwise only the <see cref="ProviderUser.Email"/> invitation is recorded), and the encrypted provider
    /// key is only stored once the membership is <see cref="ProviderUserStatusType.Confirmed"/>.
    /// The caller computes <paramref name="encryptedProviderKey"/> via
    /// <see cref="RustSdkService.GenerateUserOrganizationKey"/>.
    /// </summary>
    internal static ProviderUser CreateProviderUser(
        Provider provider,
        User user,
        ProviderUserType type,
        ProviderUserStatusType status,
        string? encryptedProviderKey)
    {
        var shouldLinkUserId = status != ProviderUserStatusType.Invited;
        var shouldIncludeKey = status == ProviderUserStatusType.Confirmed;

        return new ProviderUser
        {
            Id = CombGuid.Generate(),
            ProviderId = provider.Id,
            UserId = shouldLinkUserId ? user.Id : null,
            Email = shouldLinkUserId ? null : user.Email,
            Key = shouldIncludeKey ? encryptedProviderKey : null,
            Type = type,
            Status = status
        };
    }

    /// <summary>
    /// Creates a confirmed ProviderAdmin membership linking the owner user to the provider.
    /// The provider's symmetric key is encrypted for the owner using their public key.
    /// </summary>
    internal static ProviderUser CreateConfirmedAdmin(Provider provider, User owner, string providerKey)
    {
        return CreateProviderUser(
            provider,
            owner,
            ProviderUserType.ProviderAdmin,
            ProviderUserStatusType.Confirmed,
            RustSdkService.GenerateUserOrganizationKey(owner.PublicKey!, providerKey));
    }
}
