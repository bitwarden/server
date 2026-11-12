using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole;

/// <summary>
/// Helpers for asserting which members a write forces to resync. Members are dated back after setup, so
/// any account revision date within the last hour was bumped by the write under test.
/// </summary>
internal static class AccountRevisionDateTestHelpers
{
    public static async Task<(User User, OrganizationUser OrganizationUser)> CreateConfirmedMemberAsync(
        this IUserRepository userRepository,
        IOrganizationUserRepository organizationUserRepository,
        Organization organization)
    {
        var user = await userRepository.CreateTestUserAsync();
        var organizationUser =
            await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, user);
        return (user, organizationUser);
    }

    public static async Task<Collection> CreateCollectionWithMembersAsync(
        this ICollectionRepository collectionRepository,
        Organization organization,
        params OrganizationUser[] members)
    {
        var collection = new Collection
        {
            OrganizationId = organization.Id,
            Name = $"test {Guid.NewGuid()}",
        };
        await collectionRepository.CreateAsync(collection, [],
            members.Select(m => new CollectionAccessSelection { Id = m.Id }).ToList());
        return collection;
    }

    public static async Task BackdateAccountRevisionDatesAsync(this IUserRepository userRepository,
        params User[] users)
    {
        foreach (var user in users)
        {
            var stored = await userRepository.GetByIdAsync(user.Id);
            Assert.NotNull(stored);
            stored.AccountRevisionDate = DateTime.UtcNow.AddDays(-1);
            await userRepository.ReplaceAsync(stored);
        }
    }

    public static async Task AssertBumpedAsync(this IUserRepository userRepository, User user)
        => Assert.True(await WasBumpedAsync(userRepository, user),
            "The AccountRevisionDate is expected to be bumped.");

    public static async Task AssertNotBumpedAsync(this IUserRepository userRepository, User user)
        => Assert.False(await WasBumpedAsync(userRepository, user),
            "The AccountRevisionDate is expected to be left alone.");

    private static async Task<bool> WasBumpedAsync(IUserRepository userRepository, User user)
    {
        var actual = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(actual);
        return actual.AccountRevisionDate > DateTime.UtcNow.AddHours(-1);
    }
}
