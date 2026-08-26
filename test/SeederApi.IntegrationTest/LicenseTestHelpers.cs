using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Seeder.Services;

namespace Bit.SeederApi.IntegrationTest;

/// <summary>
/// Shared harness for the self-hosted premium license tests: a premium-owner factory plus hand-written
/// licensing stubs.
/// </summary>
internal static class LicenseTestHelpers
{
    internal static User NewPremiumOwner() => new()
    {
        Id = Guid.NewGuid(),
        Email = "premium.user@example.com",
        Premium = true,
    };

    internal sealed class StubSeederLicenseSigner(Func<User, Task<LicenseSigningResult>> behavior) : ISeederLicenseSigner
    {
        public Task<LicenseSigningResult> CreateUserTokenAsync(User user) => behavior(user);
    }

    /// <summary>
    /// Captures the licenses passed to <see cref="ILicensingService.WriteUserLicenseAsync"/> and runs
    /// <paramref name="onWrite"/> to drive the success or failure path.
    /// </summary>
    internal sealed class StubLicensingService(Func<User, UserLicense, Task> onWrite) : ILicensingService
    {
        public List<UserLicense> WrittenLicenses { get; } = [];

        public Task WriteUserLicenseAsync(User user, UserLicense license)
        {
            WrittenLicenses.Add(license);
            return onWrite(user, license);
        }

        public Task ValidateOrganizationsAsync() => throw new NotImplementedException();
        public Task ValidateUsersAsync() => throw new NotImplementedException();
        public Task<bool> ValidateUserPremiumAsync(User user) => throw new NotImplementedException();
        public bool VerifyLicense(ILicense license) => throw new NotImplementedException();
        public byte[] SignLicense(ILicense license) => throw new NotImplementedException();
        public Task<OrganizationLicense?> ReadOrganizationLicenseAsync(Organization organization) => throw new NotImplementedException();
        public Task<OrganizationLicense?> ReadOrganizationLicenseAsync(Guid organizationId) => throw new NotImplementedException();
        public ClaimsPrincipal? GetClaimsPrincipalFromLicense(ILicense license) => throw new NotImplementedException();
        public Task<string?> CreateOrganizationTokenAsync(Organization organization, Guid installationId, SubscriptionInfo subscriptionInfo) => throw new NotImplementedException();
        public Task<string?> CreateUserTokenAsync(User user, SubscriptionInfo subscriptionInfo) => throw new NotImplementedException();
    }
}
