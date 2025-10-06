using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole;

/// <summary>
/// A set of extension methods used to arrange simple test data.
/// This should only be used for basic, repetitive data arrangement, not for anything complex or for
/// the repository method under test.
/// </summary>
public static class OrganizationTestHelpers
{
    public static Task<User> CreateTestUserAsync(this IUserRepository userRepository, string identifier = "test")
    {
        var id = Guid.NewGuid();
        return userRepository.CreateAsync(new User
        {
            Id = id,
            Name = $"{identifier}-{id}",
            Email = $"{id}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });
    }

    /// <summary>
    /// Creates an Enterprise organization.
    /// </summary>
    public static Task<Organization> CreateTestOrganizationAsync(this IOrganizationRepository organizationRepository,
        int? seatCount = null,
        string identifier = "test")
    {
        var id = Guid.NewGuid();
        return organizationRepository.CreateAsync(new Organization
        {
            Name = $"{identifier}-{id}",
            BillingEmail = $"billing-{id}@example.com",
            Plan = "Enterprise (Annually)",
            PlanType = PlanType.EnterpriseAnnually,
            Identifier = $"{identifier}-{id}",
            BusinessName = $"Test Business {id}",
            BusinessAddress1 = "123 Test Street",
            BusinessAddress2 = "Suite 100",
            BusinessAddress3 = "Building A",
            BusinessCountry = "US",
            BusinessTaxNumber = "123456789",
            Seats = seatCount,
            MaxCollections = 50,
            UsePolicies = true,
            UseSso = true,
            UseKeyConnector = true,
            UseScim = true,
            UseGroups = true,
            UseDirectory = true,
            UseEvents = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            UseResetPassword = true,
            UseSecretsManager = true,
            UsePasswordManager = true,
            SelfHost = false,
            UsersGetPremium = true,
            UseCustomPermissions = true,
            Storage = 1073741824, // 1 GB in bytes
            MaxStorageGb = 10,
            Gateway = GatewayType.Stripe,
            GatewayCustomerId = $"cus_{id}",
            GatewaySubscriptionId = $"sub_{id}",
            ReferenceData = "{\"test\":\"data\"}",
            Enabled = true,
            LicenseKey = $"license-{id}",
            PublicKey = "test-public-key",
            PrivateKey = "test-private-key",
            TwoFactorProviders = null,
            ExpirationDate = DateTime.UtcNow.AddYears(1),
            MaxAutoscaleSeats = 200,
            OwnersNotifiedOfAutoscaling = null,
            Status = OrganizationStatusType.Managed,
            SmSeats = 50,
            SmServiceAccounts = 25,
            MaxAutoscaleSmSeats = 100,
            MaxAutoscaleSmServiceAccounts = 50,
            LimitCollectionCreation = true,
            LimitCollectionDeletion = true,
            LimitItemDeletion = true,
            AllowAdminAccessToAllCollectionItems = true,
            UseRiskInsights = true,
            UseOrganizationDomains = true,
            UseAdminSponsoredFamilies = true,
            SyncSeats = false
        });
    }

    /// <summary>
    /// Creates a confirmed Owner for the specified organization and user.
    /// Does not include any cryptographic material.
    /// </summary>
    public static Task<OrganizationUser> CreateTestOrganizationUserAsync(
        this IOrganizationUserRepository organizationUserRepository,
        Organization organization,
        User user)
        => organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner
        });

    public static Task<OrganizationUser> CreateTestOrganizationUserInviteAsync(
        this IOrganizationUserRepository organizationUserRepository,
        Organization organization)
        => organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null, // Invites are not linked to a UserId
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.Owner
        });

    public static Task<Group> CreateTestGroupAsync(
        this IGroupRepository groupRepository,
        Organization organization,
        string identifier = "test")
        => groupRepository.CreateAsync(
            new Group { OrganizationId = organization.Id, Name = $"{identifier} {Guid.NewGuid()}" }
        );

    public static Task<Collection> CreateTestCollectionAsync(
        this ICollectionRepository collectionRepository,
        Organization organization,
        string identifier = "test")
    => collectionRepository.CreateAsync(new Collection
    {
        OrganizationId = organization.Id,
        Name = $"{identifier} {Guid.NewGuid()}"
    });
}
