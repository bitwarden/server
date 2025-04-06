using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Identity.TokenProviders;

public interface IOrganizationTwoFactorTokenProvider
{
    Task<bool> CanGenerateTwoFactorTokenAsync(Organization organization);
    Task<string> GenerateAsync(Organization organization, User user);
    Task<bool> ValidateAsync(string token, Organization organization, User user);
}
