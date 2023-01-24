using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.Api.IntegrationTest.Helpers;

public static class OrganizationTestHelpers
{
    public static async Task<Tuple<Organization, OrganizationUser>> SignUpAsync<T>(
        WebApplicationFactoryBase<T> factory,
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

    public static async Task<OrganizationUser> CreateUserAsync<T>(
        WebApplicationFactoryBase<T> factory,
        Guid organizationId,
        string userEmail,
        OrganizationUserType type
    ) where T : class
    {
        var userRepository = factory.GetService<IUserRepository>();
        var organizationUserRepository = factory.GetService<IOrganizationUserRepository>();

        var user = await userRepository.GetByEmailAsync(userEmail);

        var orgUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            Key = null,
            Type = type,
            Status = OrganizationUserStatusType.Invited,
            AccessAll = false,
            ExternalId = null,
        };

        await organizationUserRepository.CreateAsync(orgUser);

        return orgUser;
    }
}
