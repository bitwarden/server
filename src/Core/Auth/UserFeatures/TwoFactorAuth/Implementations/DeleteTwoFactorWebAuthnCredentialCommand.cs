using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;

public class DeleteTwoFactorWebAuthnCredentialCommand : IDeleteTwoFactorWebAuthnCredentialCommand
{
    private readonly IUserService _userService;

    public DeleteTwoFactorWebAuthnCredentialCommand(IUserService userService)
    {
        _userService = userService;
    }
    public async Task<bool> DeleteTwoFactorWebAuthnCredentialAsync(User user, int id)
    {
        var providers = user.GetTwoFactorProviders();
        if (providers == null)
        {
            return false;
        }

        var keyName = $"Key{id}";
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        if (provider?.MetaData == null || !provider.MetaData.ContainsKey(keyName))
        {
            return false;
        }

        if (provider.MetaData.Count(k => k.Key.StartsWith("Key")) < 2)
        {
            return false;
        }

        provider.MetaData.Remove(keyName);
        providers[TwoFactorProviderType.WebAuthn] = provider;
        user.SetTwoFactorProviders(providers);
        await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
        return true;
    }
}
