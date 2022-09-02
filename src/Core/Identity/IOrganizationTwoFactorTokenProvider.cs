using Bit.Core.Entities;

namespace Bit.Core.Identity;

public interface IOrganizationTwoFactorTokenProvider
{
    Task<bool> CanGenerateTwoFactorTokenAsync(Organization organization);
    Task<string> GenerateAsync(Organization organization, User user);
    Task<bool> ValidateAsync(string token, Organization organization, User user);
}
