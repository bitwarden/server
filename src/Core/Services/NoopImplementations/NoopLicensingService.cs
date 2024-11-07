using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Bit.Core.Services;

public class NoopLicensingService : ILicensingService
{
    public NoopLicensingService(
        IWebHostEnvironment environment,
        GlobalSettings globalSettings)
    {
        if (!environment.IsDevelopment() && globalSettings.SelfHosted)
        {
            throw new Exception($"{nameof(NoopLicensingService)} cannot be used for self hosted instances.");
        }
    }

    public Task ValidateOrganizationsAsync()
    {
        return Task.FromResult(0);
    }

    public Task ValidateUsersAsync()
    {
        return Task.FromResult(0);
    }

    public Task<bool> ValidateUserPremiumAsync(User user)
    {
        return Task.FromResult(user.Premium);
    }

    public bool VerifyLicense(ILicense license)
    {
        return true;
    }

    public byte[] SignLicense(ILicense license)
    {
        return new byte[0];
    }

    public Task<OrganizationLicense> ReadOrganizationLicenseAsync(Organization organization)
    {
        return Task.FromResult<OrganizationLicense>(null);
    }

    public Task<OrganizationLicense> ReadOrganizationLicenseAsync(Guid organizationId)
    {
        return Task.FromResult<OrganizationLicense>(null);
    }

    public ClaimsPrincipal GetClaimsPrincipalFromLicense(ILicense license)
    {
        return null;
    }

    public Task<string> CreateOrganizationTokenAsync(Organization organization, Guid installationId, SubscriptionInfo subscriptionInfo)
    {
        return Task.FromResult<string>(null);
    }

    public Task<string> CreateUserTokenAsync(User user, SubscriptionInfo subscriptionInfo)
    {
        return Task.FromResult<string>(null);
    }
}
