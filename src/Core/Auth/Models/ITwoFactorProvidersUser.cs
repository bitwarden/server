using Bit.Core.Auth.Enums;

namespace Bit.Core.Auth.Models;

public interface ITwoFactorProvidersUser
{
    string TwoFactorProviders { get; }
    Dictionary<TwoFactorProviderType, TwoFactorProvider> GetTwoFactorProviders();
    Guid? GetUserId();
    bool GetPremium();
}
