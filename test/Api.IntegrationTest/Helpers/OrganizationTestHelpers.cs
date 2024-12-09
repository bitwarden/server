using System.Diagnostics;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.Api.IntegrationTest.Helpers;

public static class OrganizationTestHelpers
{
    public static async Task<Tuple<Organization, OrganizationUser>> SignUpAsync<T>(WebApplicationFactoryBase<T> factory,
        PlanType plan = PlanType.Free,
        string ownerEmail = "integration-test@bitwarden.com",
        string name = "Integration Test Org",
        string billingEmail = "integration-test@bitwarden.com",
        string ownerKey = "test-key",
        int passwordManagerSeats = 0,
        PaymentMethodType paymentMethod = PaymentMethodType.None) where T : class
    {
        var userRepository = factory.GetService<IUserRepository>();
        var organizationSignUpCommand = factory.GetService<ICloudOrganizationSignUpCommand>();

        var owner = await userRepository.GetByEmailAsync(ownerEmail);

        var signUpResult = await organizationSignUpCommand.SignUpOrganizationAsync(new OrganizationSignup
        {
            Name = name,
            BillingEmail = billingEmail,
            Plan = plan,
            OwnerKey = ownerKey,
            Owner = owner,
            AdditionalSeats = passwordManagerSeats,
            PaymentMethodType = paymentMethod
        });

        Debug.Assert(signUpResult.OrganizationUser is not null);

        return new Tuple<Organization, OrganizationUser>(signUpResult.Organization, signUpResult.OrganizationUser);
    }

    /// <summary>
    /// Creates an OrganizationUser. The user account must already be created.
    /// </summary>
    public static async Task<OrganizationUser> CreateUserAsync<T>(
        WebApplicationFactoryBase<T> factory,
        Guid organizationId,
        string userEmail,
        OrganizationUserType type,
        bool accessSecretsManager = false,
        Permissions? permissions = null
    ) where T : class
    {
        var userRepository = factory.GetService<IUserRepository>();
        var organizationUserRepository = factory.GetService<IOrganizationUserRepository>();

        var user = await userRepository.GetByEmailAsync(userEmail);
        Debug.Assert(user is not null);

        var orgUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            Key = null,
            Type = type,
            Status = OrganizationUserStatusType.Confirmed,
            ExternalId = null,
            AccessSecretsManager = accessSecretsManager,
        };

        if (permissions != null)
        {
            orgUser.SetPermissions(permissions);
        }

        await organizationUserRepository.CreateAsync(orgUser);

        return orgUser;
    }

    /// <summary>
    /// Creates a new User account with a unique email address and a corresponding OrganizationUser for
    /// the specified organization.
    /// </summary>
    public static async Task<(string, OrganizationUser)> CreateNewUserWithAccountAsync(
        ApiApplicationFactory factory,
        Guid organizationId,
        OrganizationUserType userType,
        Permissions? permissions = null
    )
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";

        // Create user
        await factory.LoginWithNewAccount(email);

        // Create organizationUser
        var organizationUser = await OrganizationTestHelpers.CreateUserAsync(factory, organizationId, email, userType,
            permissions: permissions);

        return (email, organizationUser);
    }

    /// <summary>
    /// Creates a VerifiedDomain for the specified organization.
    /// </summary>
    public static async Task CreateVerifiedDomainAsync(ApiApplicationFactory factory, Guid organizationId, string domain)
    {
        var organizationDomainRepository = factory.GetService<IOrganizationDomainRepository>();

        var verifiedDomain = new OrganizationDomain
        {
            OrganizationId = organizationId,
            DomainName = domain,
            Txt = "btw+test18383838383"
        };
        verifiedDomain.SetVerifiedDate();

        await organizationDomainRepository.CreateAsync(verifiedDomain);
    }
}
