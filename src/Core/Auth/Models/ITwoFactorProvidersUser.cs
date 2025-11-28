// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Services;

namespace Bit.Core.Auth.Models;

public interface ITwoFactorProvidersUser
{
    string TwoFactorProviders { get; }
    /// <summary>
    /// Get the two factor providers for the user. Currently it can be assumed providers are enabled
    /// if they exists in the dictionary. When two factor providers are disabled they are removed
    /// from the dictionary. <see cref="IUserService.DisableTwoFactorProviderAsync"/>
    /// <see cref="IOrganizationService.DisableTwoFactorProviderAsync"/>
    /// </summary>
    /// <returns>Dictionary of providers with the type enum as the key</returns>
    Dictionary<TwoFactorProviderType, TwoFactorProvider> GetTwoFactorProviders();
    Guid? GetUserId();
    bool GetPremium();
}
