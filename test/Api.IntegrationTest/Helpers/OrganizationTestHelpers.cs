using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.Api.IntegrationTest.Helpers;

public static class OrganizationTestHelpers
{
    public static async Task<Tuple<Organization, OrganizationUser>> SignUpAsync<T>(WebApplicationFactoryBase<T> factory,
        PlanType plan = PlanType.Free,
        string ownerEmail = "integration-test@bitwarden.com",
        string name = "Integration Test Org",
        string billingEmail = "integration-test@bitwarden.com",
        string ownerKey = "test-key") where T : class
    {
        var userRepository = factory.GetService<IUserRepository>();
        var organizationService = factory.GetService<IOrganizationService>();

        var owner = await userRepository.GetByEmailAsync(ownerEmail);

        return await organizationService.SignUpAsync(new OrganizationSignup
        {
            Name = name,
            BillingEmail = billingEmail,
            Plan = plan,
            OwnerKey = ownerKey,
            Owner = owner,
        });
    }
}
