using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

/// <summary>
/// Runs the member adoption report against every configured database: the Dapper stored procedure on SQL Server and
/// the LINQ translation on MySQL, PostgreSQL and SQLite.
/// </summary>
public class MemberAdoptionReportRepositoryTests
{
    private static readonly DateTime _olderActivityDate = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _latestActivityDate = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Theory, DatabaseData]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_ReportsAdoptionForEveryConfirmedMember(
        IMemberAdoptionReportRepository sut,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository,
        IOrganizationSponsorshipRepository organizationSponsorshipRepository,
        IDeviceRepository deviceRepository)
    {
        var organization = await organizationRepository.CreateAsync(BuildOrganization("Reported organization"));
        var sponsoredOrganization = await organizationRepository.CreateAsync(BuildOrganization("Sponsored organization"));

        var memberUser = await userRepository.CreateAsync(BuildUser("member"));
        var memberWithoutAccessUser = await userRepository.CreateAsync(BuildUser("no-access"));
        var invitedUser = await userRepository.CreateAsync(BuildUser("invited"));

        var member = await organizationUserRepository.CreateAsync(
            BuildOrganizationUser(organization.Id, memberUser.Id, OrganizationUserStatusType.Confirmed));
        var memberWithoutAccess = await organizationUserRepository.CreateAsync(
            BuildOrganizationUser(organization.Id, memberWithoutAccessUser.Id, OrganizationUserStatusType.Confirmed));
        var invited = await organizationUserRepository.CreateAsync(
            BuildOrganizationUser(organization.Id, invitedUser.Id, OrganizationUserStatusType.Invited));

        // The member's newest activity is on a desktop, not the extension.
        await deviceRepository.CreateAsync(
            BuildDevice(memberUser.Id, DeviceType.ChromeExtension, _olderActivityDate));
        await deviceRepository.CreateAsync(
            BuildDevice(memberUser.Id, DeviceType.MacOsDesktop, _latestActivityDate));
        await deviceRepository.CreateAsync(
            BuildDevice(memberWithoutAccessUser.Id, DeviceType.MacOsDesktop, _olderActivityDate));

        var sharedCollection = await collectionRepository.CreateAsync(
            BuildCollection(organization.Id, "Direct and group access"));
        var groupOnlyCollection = await collectionRepository.CreateAsync(
            BuildCollection(organization.Id, "Group access only"));
        var directOnlyCollection = await collectionRepository.CreateAsync(
            BuildCollection(organization.Id, "Direct access only"));

        var group = new Group { OrganizationId = organization.Id, Name = "Engineering" };
        await groupRepository.CreateAsync(group,
        [
            new CollectionAccessSelection { Id = sharedCollection.Id, Manage = true },
            new CollectionAccessSelection { Id = groupOnlyCollection.Id, Manage = true }
        ]);
        await groupRepository.AddGroupUsersByIdAsync(group.Id, [member.Id], DateTime.UtcNow);

        await collectionRepository.UpdateUsersAsync(sharedCollection.Id,
            [new CollectionAccessSelection { Id = member.Id, Manage = true }]);
        await collectionRepository.UpdateUsersAsync(directOnlyCollection.Id,
            [new CollectionAccessSelection { Id = member.Id, Manage = true }]);

        var cipherReachableByBothPaths = await cipherRepository.CreateAsync(
            BuildCipher(userId: null, organizationId: organization.Id));
        var cipherReachableByDirectAccessOnly = await cipherRepository.CreateAsync(
            BuildCipher(userId: null, organizationId: organization.Id));
        var cipherReachableByGroupAccessOnly = await cipherRepository.CreateAsync(
            BuildCipher(userId: null, organizationId: organization.Id));
        var deletedCipher = await cipherRepository.CreateAsync(
            BuildCipher(userId: null, organizationId: organization.Id, deletedDate: _latestActivityDate));

        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(organization.Id,
            [cipherReachableByBothPaths.Id], [sharedCollection.Id, groupOnlyCollection.Id]);
        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(organization.Id,
            [cipherReachableByDirectAccessOnly.Id], [directOnlyCollection.Id]);
        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(organization.Id,
            [cipherReachableByGroupAccessOnly.Id], [groupOnlyCollection.Id]);
        await collectionCipherRepository.AddCollectionsForManyCiphersAsync(organization.Id,
            [deletedCipher.Id], [sharedCollection.Id]);

        await cipherRepository.CreateAsync(BuildCipher(memberUser.Id, organizationId: null));
        await cipherRepository.CreateAsync(
            BuildCipher(memberUser.Id, organizationId: null, deletedDate: _latestActivityDate));
        await cipherRepository.CreateAsync(BuildCipher(memberUser.Id, organizationId: organization.Id));

        await organizationSponsorshipRepository.CreateAsync(new OrganizationSponsorship
        {
            SponsoringOrganizationId = organization.Id,
            SponsoringOrganizationUserId = member.Id,
            SponsoredOrganizationId = sponsoredOrganization.Id,
            OfferedToEmail = memberUser.Email
        });
        await organizationSponsorshipRepository.CreateAsync(new OrganizationSponsorship
        {
            SponsoringOrganizationId = organization.Id,
            SponsoringOrganizationUserId = memberWithoutAccess.Id,
            SponsoredOrganizationId = null,
            OfferedToEmail = memberWithoutAccessUser.Email
        });

        var details = (await sut.GetMemberAdoptionDetailsByOrganizationIdAsync(organization.Id)).ToList();

        Assert.Equal(2, details.Count);
        Assert.DoesNotContain(details, detail => detail.OrganizationUserId == invited.Id);

        var memberDetail = Single(details, member.Id);
        Assert.Equal(memberUser.Id, memberDetail.UserId);
        Assert.Equal(memberUser.Email, memberDetail.Email);
        Assert.Equal(_latestActivityDate, memberDetail.LastActivityDate);
        Assert.True(memberDetail.HasExtensionInstalled);
        Assert.Equal(1, memberDetail.VaultItemCount);
        Assert.Equal(3, memberDetail.SharedItemCount);
        Assert.True(memberDetail.HasRedeemedSponsorship);

        var memberWithoutAccessDetail = Single(details, memberWithoutAccess.Id);
        Assert.Equal(_olderActivityDate, memberWithoutAccessDetail.LastActivityDate);
        Assert.False(memberWithoutAccessDetail.HasExtensionInstalled);
        Assert.Equal(0, memberWithoutAccessDetail.VaultItemCount);
        Assert.Equal(0, memberWithoutAccessDetail.SharedItemCount);
        Assert.False(memberWithoutAccessDetail.HasRedeemedSponsorship);
    }

    private static MemberAdoptionReportDetail Single(
        IEnumerable<MemberAdoptionReportDetail> details, Guid organizationUserId) =>
        details.Single(detail => detail.OrganizationUserId == organizationUserId);

    private static Organization BuildOrganization(string name) =>
        new()
        {
            Name = name,
            PlanType = PlanType.EnterpriseAnnually,
            Plan = "Enterprise",
            BillingEmail = $"billing+{Guid.NewGuid()}@example.com",
            UseGroups = true
        };

    private static User BuildUser(string prefix) =>
        new()
        {
            Name = prefix,
            Email = $"{prefix}+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp"
        };

    private static OrganizationUser BuildOrganizationUser(
        Guid organizationId, Guid userId, OrganizationUserStatusType status) =>
        new()
        {
            OrganizationId = organizationId,
            UserId = userId,
            Status = status,
            Type = OrganizationUserType.User
        };

    private static Device BuildDevice(Guid userId, DeviceType type, DateTime lastActivityDate) =>
        new()
        {
            UserId = userId,
            Name = type.ToString(),
            Identifier = Guid.NewGuid().ToString(),
            Type = type,
            LastActivityDate = lastActivityDate
        };

    private static Collection BuildCollection(Guid organizationId, string name) =>
        new()
        {
            OrganizationId = organizationId,
            Name = name,
            Type = CollectionType.SharedCollection
        };

    private static Cipher BuildCipher(Guid? userId, Guid? organizationId, DateTime? deletedDate = null) =>
        new()
        {
            UserId = userId,
            OrganizationId = organizationId,
            Type = CipherType.Login,
            Data = "",
            DeletedDate = deletedDate
        };
}
