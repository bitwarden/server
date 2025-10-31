using System.Diagnostics;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.Repositories;
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
            PaymentMethodType = paymentMethod,
            PaymentToken = "TOKEN",
            TaxInfo = new TaxInfo
            {
                BillingAddressCountry = "US",
                BillingAddressPostalCode = "12345"
            }
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
        Permissions? permissions = null,
        OrganizationUserStatusType userStatusType = OrganizationUserStatusType.Confirmed,
        string? externalId = null
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
            Status = userStatusType,
            ExternalId = externalId,
            AccessSecretsManager = accessSecretsManager,
            Email = userEmail
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
        var organizationUser = await CreateUserAsync(factory, organizationId, email, userType,
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

    public static async Task<Group> CreateGroup(ApiApplicationFactory factory, Guid organizationId)
    {

        var groupRepository = factory.GetService<IGroupRepository>();
        var group = new Group
        {
            OrganizationId = organizationId,
            Id = new Guid(),
            ExternalId = "bwtest-externalId",
            Name = "bwtest"
        };

        await groupRepository.CreateAsync(group, new List<CollectionAccessSelection>());
        return group;
    }

    /// <summary>
    /// Enables the Organization Data Ownership policy for the specified organization.
    /// </summary>
    public static async Task EnableOrganizationDataOwnershipPolicyAsync<T>(
        WebApplicationFactoryBase<T> factory,
        Guid organizationId) where T : class
    {
        var policyRepository = factory.GetService<IPolicyRepository>();

        var policy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.OrganizationDataOwnership,
            Enabled = true
        };

        await policyRepository.CreateAsync(policy);
    }

    /// <summary>
    /// Enables the Organization Auto Confirm policy for the specified organization.
    /// </summary>
    public static async Task EnableOrganizationAutoConfirmPolicyAsync<T>(
        WebApplicationFactoryBase<T> factory,
        Guid organizationId) where T : class
    {
        var policyRepository = factory.GetService<IPolicyRepository>();

        var policy = new Policy
        {
            OrganizationId = organizationId,
            Type = PolicyType.AutomaticUserConfirmation,
            Enabled = true
        };

        await policyRepository.CreateAsync(policy);
    }

    /// <summary>
    /// Creates a user account without a Master Password and adds them as a member to the specified organization.
    /// </summary>
    public static async Task<(User User, OrganizationUser OrganizationUser)> CreateUserWithoutMasterPasswordAsync(ApiApplicationFactory factory, string email, Guid organizationId)
    {
        var userRepository = factory.GetService<IUserRepository>();
        var user = await userRepository.CreateAsync(new User
        {
            Email = email,
            Culture = "en-US",
            SecurityStamp = "D7ZH62BWAZ5R5CASKULCDDIQGKDA2EJ6",
            PublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwMj7W00xS7H0NWasGn7PfEq8VfH3fa5XuZucsKxLLRAHHZk0xGRZJH2lFIznizv3GpF8vzhHhe9VpmMkrdIa5oWhwHpy+D7Z1QCQxuUXzvMKpa95GOntr89nN/mWKpk6abjgjmDcqFJ0lhDqkKnDfes+d8BBd5oEA8p41/Ykz7OfG7AiktVBpTQFW09MQh1NOvcLxVgiUUVRPwNRKrOeCekWDtOjZhASMETv3kI1ogvhHukOQ3ztDzrxvmwnLQ+cXl1EeD8gQnGDp3QLiJqxPgh2EdmANh4IzjRexoDn6BqhRGqLLIoLAbbkoiNrd6NYujrWW0N8KMMoVEXuJL2g4wIDAQAB",
            PrivateKey = "2.Ytudv+Qk3ET9hN8whqpuGg==|ijsFhmjaf1aaT9uz+IPhVTzMS+2W/ldAP8LdT5VyJaFdx4HSdLcWSZvz5xWuuW94zfv1Qh+p3iQIuZOr29G4jcx47rYtz4ssiFtB7Ia552ZeF+cb7uuVg40CIe7ycuJQITk00o8gots+wFnaEvk0Vjgycnqutm0jpeBJ1joWJWqTVgSsYdUGLu7PiJywQ9NgY4+bJXqadlcviS3rhPKJXtiXYJhqJqSw+vI0Yxp96MJ0HcFJk/LG22YJPTvL5kzuDq/Wzj40kj8blQ+ag+xHD4P/KJ/MppEB3OpDw3UoJ50Ek+YB9pOqGxZtvqMEzBDsgh0yoz1O992UnhaUqtJ5e9Bxy3PA6cJsdyn9npduNOreEb8vePCidN2XC+chjJpPFpjms9muHLKgfaTIfpiJA2Tz8E9dvSyhHHTE1mY+xEA7P08BYKN3LNoSGIjdiZuouJ1V/KZvCssDfVG1tli2qpnhTIh4m3rAMhbM8WW3B7wCV8N0MpcJJSvndkVcMgRbgWcbivLeXuKdE/K98n01RvOLSJyslhLGCGEQQKw6N3HQ2iELfv84YQZi2fjDK+OqAmXDq1pNcjKX2I8dqBwl31tPC8qSZiWnfinwLdqQTvSQjOIyAHb4sSjAwgdMbCRzUTChRr09l+PAZqGWdMC5N2Bw+bA8WP0l2Wdxuv9Abxl3F7xGeAA9Rw9PU5wGKujaMRmO4V9MFjNyyCcw4D9pzKMW6OUKsHsHE7tsG7KskCzksHzrZGawAt0S41BYQA/JwePCrD3F6dM92anlC1LfA00KJb0tmFdU0yJNmJfR+S78yn8yM6wDgIs2cFB3W1fYfpfUvQm+zzPoEQihNxBxnwFsBtMAOtPy54FjSzKmxsQTrYT9E6NFb8k6ZIIm2gNeOPK9OUJgjw+4g2BXErM6ikHTzM3xcaTq/cQaePZ52emndw1qOtdV06hr2EeuLM8frfLHpsknUe8JeYeW5p9E8QdZjjSN9034usdYNamUdxzmn/Mw/ar8z1xSKS6zcaQoTQ7aYLEX3dWJndc4W64HyiaRkLjO6qLUFeOerfz5UvcxxRY89eAA0KLC2xnGkBMOhXxYzIB3lF8Zxqb4JMhoBGw1n31TDfhRDGDHHEAsZuAIcH7aC5RDVxU08Jxmw4oLmeTDZA5BFcqp2A3fusNVZUnfpmMy6DCJyFprlRl8jSlJMAvhbxVuuLFDZnjl77Z2of796Ur6DgmNwYtMPNEntZPIcZ76VPLWAL8lqiRBm20c4qiwr5rNSr5kry9bR1EfXHwFRjy5pxFQ+5+ilpRl8WPfT/iUuORd8J2wnCmghm7uxiJd9t82kX0s6benhL29dQ1etqt5soX2RnlfKan16GVWoI3xrljIQrCAY4xpdptSpglOnrpSClbN1nhGkDfFPNq2pWhQrDbznDknAJ9MxQaVnLYPhn7I849GMd7EvpSkydwQu7QXn9+H4jxn6UEntNGxcL0xkG+xippvZEe+HBvcDD40efDQW1bDbILLjPb4rNRx4d3xaQnVNaF7L33osm5LgfXAQSwHJiURdkU4zmhtPP4zn0br0OdFlR3mPcrkeNeSvs7FxiKtD6n6s+av+4bKjbLL1OyuwmTnMilL6p+m8ldte0yos/r+zOuxWeI=|euhiXWXehYbFQhlAV6LIECSIPCIRaHbNdr9OI4cTPUM=",
            ApiKey = "CfGrD4MoJu3NprOBZNL8tu5ocmtnmU",
            KdfIterations = 600000
        });

        var organizationUser = await CreateUserAsync(factory, organizationId, user.Email,
            OrganizationUserType.User, externalId: email);

        return (user, organizationUser);
    }
}
