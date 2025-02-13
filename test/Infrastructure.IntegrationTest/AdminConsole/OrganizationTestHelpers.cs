using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
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

    public static Task<Organization> CreateTestOrganizationAsync(this IOrganizationRepository organizationRepository,
        string identifier = "test")
        => organizationRepository.CreateAsync(new Organization
        {
            Name = $"{identifier}-{Guid.NewGuid()}",
            BillingEmail = "billing@example.com", // TODO: EF does not enforce this being NOT NULL
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
        });

    public static Task<OrganizationUser> CreateTestOrganizationUserAsync(
        this IOrganizationUserRepository organizationUserRepository,
        Organization organization,
        User user,
        OrganizationUserType role = OrganizationUserType.Owner)
        => organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = role
        });

    public static Task<Group> CreateTestGroupAsync(
        this IGroupRepository groupRepository,
        Organization organization,
        string identifier = "test")
        => groupRepository.CreateAsync(
            new Group { OrganizationId = organization.Id, Name = $"{identifier} {Guid.NewGuid()}" }
        );
}
