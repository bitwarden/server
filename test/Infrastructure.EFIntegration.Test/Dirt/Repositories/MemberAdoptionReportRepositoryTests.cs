using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Vault.Entities;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Xunit;
using EfAdminConsoleRepo = Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;
using EfDirtRepo = Bit.Infrastructure.EntityFramework.Dirt.Repositories;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using EfVaultRepo = Bit.Infrastructure.EntityFramework.Vault.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Dirt.Repositories;

public class MemberAdoptionReportRepositoryTests
{
    private static readonly DateTime _olderActivityDate = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _latestActivityDate = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _deletionDate = new(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);

    [CiSkippedTheory, EfMemberAdoptionReportAutoData]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_ForAllEFProviders_ReturnsConfirmedMemberDetail(
        User user,
        Organization organization,
        OrganizationUser organizationUser,
        Device extensionDevice,
        Device desktopDevice,
        List<EfDirtRepo.MemberAdoptionReportRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        List<EfAdminConsoleRepo.OrganizationUserRepository> efOrganizationUserRepos,
        List<EfRepo.DeviceRepository> efDeviceRepos)
    {
        var results = new List<(Guid OrganizationUserId, Guid UserId, MemberAdoptionReportDetail Detail)>();

        foreach (var sut in suts)
        {
            var index = suts.IndexOf(sut);

            var (efUser, efOrganization, efOrganizationUser) = await SeedConfirmedMemberAsync(
                index, user, organization, organizationUser,
                efUserRepos, efOrganizationRepos, efOrganizationUserRepos);

            // The member's newest activity is on a desktop, not the extension.
            await SeedDeviceAsync(index, efDeviceRepos, extensionDevice, efUser.Id,
                DeviceType.ChromeExtension, _olderActivityDate);
            await SeedDeviceAsync(index, efDeviceRepos, desktopDevice, efUser.Id,
                DeviceType.MacOsDesktop, _latestActivityDate);

            var details = await sut.GetMemberAdoptionDetailsByOrganizationIdAsync(efOrganization.Id);

            var detail = Assert.Single(details);
            results.Add((efOrganizationUser.Id, efUser.Id, detail));
        }

        Assert.NotEmpty(results);
        foreach (var (organizationUserId, userId, detail) in results)
        {
            Assert.Equal(organizationUserId, detail.OrganizationUserId);
            Assert.Equal(userId, detail.UserId);
            Assert.Equal(user.Email, detail.Email);
            Assert.Equal(user.Name, detail.Name);
            Assert.Equal(_latestActivityDate, detail.LastActivityDate);
            Assert.True(detail.HasExtensionInstalled);
            Assert.Equal(0, detail.VaultItemCount);
            Assert.Equal(0, detail.SharedItemCount);
            Assert.False(detail.HasRedeemedSponsorship);
        }
    }

    [CiSkippedTheory, EfMemberAdoptionReportAutoData]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_ForAllEFProviders_ExcludesMembersWhoAreNotConfirmed(
        User user,
        Organization organization,
        OrganizationUser organizationUser,
        List<EfDirtRepo.MemberAdoptionReportRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        List<EfAdminConsoleRepo.OrganizationUserRepository> efOrganizationUserRepos)
    {
        var resultSets = new List<IEnumerable<MemberAdoptionReportDetail>>();

        foreach (var sut in suts)
        {
            var index = suts.IndexOf(sut);

            var (_, efOrganization, _) = await SeedConfirmedMemberAsync(
                index, user, organization, organizationUser,
                efUserRepos, efOrganizationRepos, efOrganizationUserRepos,
                OrganizationUserStatusType.Accepted);

            resultSets.Add(await sut.GetMemberAdoptionDetailsByOrganizationIdAsync(efOrganization.Id));
        }

        Assert.NotEmpty(resultSets);
        Assert.All(resultSets, Assert.Empty);
    }

    [CiSkippedTheory, EfMemberAdoptionReportAutoData]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_ForAllEFProviders_ReportsNoActivityWhenMemberHasNoDevices(
        User user,
        Organization organization,
        OrganizationUser organizationUser,
        List<EfDirtRepo.MemberAdoptionReportRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        List<EfAdminConsoleRepo.OrganizationUserRepository> efOrganizationUserRepos)
    {
        var details = new List<MemberAdoptionReportDetail>();

        foreach (var sut in suts)
        {
            var index = suts.IndexOf(sut);

            var (_, efOrganization, _) = await SeedConfirmedMemberAsync(
                index, user, organization, organizationUser,
                efUserRepos, efOrganizationRepos, efOrganizationUserRepos);

            details.Add(Assert.Single(await sut.GetMemberAdoptionDetailsByOrganizationIdAsync(efOrganization.Id)));
        }

        Assert.NotEmpty(details);
        foreach (var detail in details)
        {
            Assert.Null(detail.LastActivityDate);
            Assert.False(detail.HasExtensionInstalled);
        }
    }

    [CiSkippedTheory, EfMemberAdoptionReportAutoData]
    public async Task GetMemberAdoptionDetailsByOrganizationIdAsync_ForAllEFProviders_CountsItemsReachableByBothAccessPathsOnce(
        User memberUser,
        User memberWithoutAccessUser,
        Organization organization,
        Organization sponsoredOrganization,
        OrganizationUser memberOrganizationUser,
        OrganizationUser memberWithoutAccessOrganizationUser,
        Collection sharedCollection,
        Collection groupOnlyCollection,
        Collection directOnlyCollection,
        Group group,
        Cipher cipherTemplate,
        OrganizationSponsorship redeemedSponsorship,
        OrganizationSponsorship offeredSponsorship,
        List<EfDirtRepo.MemberAdoptionReportRepository> suts,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfRepo.OrganizationRepository> efOrganizationRepos,
        List<EfAdminConsoleRepo.OrganizationUserRepository> efOrganizationUserRepos,
        List<EfAdminConsoleRepo.CollectionRepository> efCollectionRepos,
        List<EfAdminConsoleRepo.GroupRepository> efGroupRepos,
        List<EfVaultRepo.CipherRepository> efCipherRepos,
        List<EfRepo.CollectionCipherRepository> efCollectionCipherRepos,
        List<EfRepo.OrganizationSponsorshipRepository> efOrganizationSponsorshipRepos)
    {
        var results = new List<(MemberAdoptionReportDetail Member, MemberAdoptionReportDetail MemberWithoutAccess)>();

        foreach (var sut in suts)
        {
            var index = suts.IndexOf(sut);

            var (efMemberUser, efOrganization, efMemberOrganizationUser) = await SeedConfirmedMemberAsync(
                index, memberUser, organization, memberOrganizationUser,
                efUserRepos, efOrganizationRepos, efOrganizationUserRepos);

            var (_, efMemberWithoutAccessOrganizationUser) = await SeedConfirmedOrganizationUserAsync(
                index, memberWithoutAccessUser, efOrganization.Id, memberWithoutAccessOrganizationUser,
                efUserRepos, efOrganizationUserRepos);

            var efSponsoredOrganization = await efOrganizationRepos[index].CreateAsync(sponsoredOrganization);
            efOrganizationRepos[index].ClearChangeTracking();

            group.OrganizationId = efOrganization.Id;
            var efGroup = await efGroupRepos[index].CreateAsync(group);
            efGroupRepos[index].ClearChangeTracking();

            var directAccess = new[] { new CollectionAccessSelection { Id = efMemberOrganizationUser.Id } };
            var groupAccess = new[] { new CollectionAccessSelection { Id = efGroup.Id } };

            await SeedCollectionAsync(index, efCollectionRepos, sharedCollection, efOrganization.Id,
                groupAccess, directAccess);
            await SeedCollectionAsync(index, efCollectionRepos, groupOnlyCollection, efOrganization.Id,
                groupAccess, users: null);
            await SeedCollectionAsync(index, efCollectionRepos, directOnlyCollection, efOrganization.Id,
                groups: null, users: directAccess);

            await efGroupRepos[index].AddGroupUsersByIdAsync(
                efGroup.Id, new[] { efMemberOrganizationUser.Id }, DateTime.UtcNow);
            efGroupRepos[index].ClearChangeTracking();

            var cipherReachableByBothPaths = await SeedCipherAsync(
                index, efCipherRepos, cipherTemplate, userId: null, organizationId: efOrganization.Id);
            var cipherReachableByGroupAccessOnly = await SeedCipherAsync(
                index, efCipherRepos, cipherTemplate, userId: null, organizationId: efOrganization.Id);
            var cipherReachableByDirectAccessOnly = await SeedCipherAsync(
                index, efCipherRepos, cipherTemplate, userId: null, organizationId: efOrganization.Id);
            var deletedCipher = await SeedCipherAsync(
                index, efCipherRepos, cipherTemplate, userId: null, organizationId: efOrganization.Id,
                deletedDate: _deletionDate);

            await SeedCipherAsync(index, efCipherRepos, cipherTemplate,
                userId: efMemberUser.Id, organizationId: efOrganization.Id);
            await SeedCipherAsync(index, efCipherRepos, cipherTemplate,
                userId: efMemberUser.Id, organizationId: null);
            await SeedCipherAsync(index, efCipherRepos, cipherTemplate,
                userId: efMemberUser.Id, organizationId: null, deletedDate: _deletionDate);

            await SeedCollectionCipherAsync(index, efCollectionCipherRepos,
                sharedCollection.Id, cipherReachableByBothPaths.Id);
            await SeedCollectionCipherAsync(index, efCollectionCipherRepos,
                groupOnlyCollection.Id, cipherReachableByBothPaths.Id);
            await SeedCollectionCipherAsync(index, efCollectionCipherRepos,
                groupOnlyCollection.Id, cipherReachableByGroupAccessOnly.Id);
            await SeedCollectionCipherAsync(index, efCollectionCipherRepos,
                directOnlyCollection.Id, cipherReachableByDirectAccessOnly.Id);
            await SeedCollectionCipherAsync(index, efCollectionCipherRepos,
                sharedCollection.Id, deletedCipher.Id);

            await SeedSponsorshipAsync(index, efOrganizationSponsorshipRepos, redeemedSponsorship,
                efOrganization.Id, efMemberOrganizationUser.Id, efSponsoredOrganization.Id);
            await SeedSponsorshipAsync(index, efOrganizationSponsorshipRepos, offeredSponsorship,
                efOrganization.Id, efMemberWithoutAccessOrganizationUser.Id, sponsoredOrganizationId: null);

            var details = (await sut.GetMemberAdoptionDetailsByOrganizationIdAsync(efOrganization.Id)).ToList();

            Assert.Equal(2, details.Count);
            results.Add((
                details.Single(detail => detail.OrganizationUserId == efMemberOrganizationUser.Id),
                details.Single(detail => detail.OrganizationUserId == efMemberWithoutAccessOrganizationUser.Id)));
        }

        Assert.NotEmpty(results);
        foreach (var (member, memberWithoutAccess) in results)
        {
            Assert.Equal(1, member.VaultItemCount);
            Assert.Equal(3, member.SharedItemCount);
            Assert.True(member.HasRedeemedSponsorship);

            Assert.Equal(0, memberWithoutAccess.VaultItemCount);
            Assert.Equal(0, memberWithoutAccess.SharedItemCount);
            Assert.False(memberWithoutAccess.HasRedeemedSponsorship);
        }
    }

    private static async Task<(User User, Organization Organization, OrganizationUser OrganizationUser)>
        SeedConfirmedMemberAsync(
            int index,
            User user,
            Organization organization,
            OrganizationUser organizationUser,
            List<EfRepo.UserRepository> efUserRepos,
            List<EfRepo.OrganizationRepository> efOrganizationRepos,
            List<EfAdminConsoleRepo.OrganizationUserRepository> efOrganizationUserRepos,
            OrganizationUserStatusType status = OrganizationUserStatusType.Confirmed)
    {
        var efOrganization = await efOrganizationRepos[index].CreateAsync(organization);
        efOrganizationRepos[index].ClearChangeTracking();

        var (efUser, efOrganizationUser) = await SeedConfirmedOrganizationUserAsync(
            index, user, efOrganization.Id, organizationUser, efUserRepos, efOrganizationUserRepos, status);

        return (efUser, efOrganization, efOrganizationUser);
    }

    private static async Task<(User User, OrganizationUser OrganizationUser)> SeedConfirmedOrganizationUserAsync(
        int index,
        User user,
        Guid organizationId,
        OrganizationUser organizationUser,
        List<EfRepo.UserRepository> efUserRepos,
        List<EfAdminConsoleRepo.OrganizationUserRepository> efOrganizationUserRepos,
        OrganizationUserStatusType status = OrganizationUserStatusType.Confirmed)
    {
        var efUser = await efUserRepos[index].CreateAsync(user);
        efUserRepos[index].ClearChangeTracking();

        organizationUser.UserId = efUser.Id;
        organizationUser.OrganizationId = organizationId;
        organizationUser.Status = status;
        var efOrganizationUser = await efOrganizationUserRepos[index].CreateAsync(organizationUser);
        efOrganizationUserRepos[index].ClearChangeTracking();

        return (efUser, efOrganizationUser);
    }

    private static async Task SeedDeviceAsync(
        int index,
        List<EfRepo.DeviceRepository> efDeviceRepos,
        Device device,
        Guid userId,
        DeviceType type,
        DateTime lastActivityDate)
    {
        device.UserId = userId;
        device.Type = type;
        device.LastActivityDate = lastActivityDate;
        await efDeviceRepos[index].CreateAsync(device);
        efDeviceRepos[index].ClearChangeTracking();
    }

    private static async Task SeedCollectionAsync(
        int index,
        List<EfAdminConsoleRepo.CollectionRepository> efCollectionRepos,
        Collection collection,
        Guid organizationId,
        IEnumerable<CollectionAccessSelection>? groups,
        IEnumerable<CollectionAccessSelection>? users)
    {
        collection.OrganizationId = organizationId;
        collection.Type = CollectionType.SharedCollection;
        collection.DefaultUserCollectionEmail = null;
        await efCollectionRepos[index].CreateAsync(collection, groups, users);
        efCollectionRepos[index].ClearChangeTracking();
    }

    private static async Task<Cipher> SeedCipherAsync(
        int index,
        List<EfVaultRepo.CipherRepository> efCipherRepos,
        Cipher template,
        Guid? userId,
        Guid? organizationId,
        DateTime? deletedDate = null)
    {
        var cipher = template.Clone();
        cipher.UserId = userId;
        cipher.OrganizationId = organizationId;
        cipher.DeletedDate = deletedDate;

        var efCipher = await efCipherRepos[index].CreateAsync(cipher);
        efCipherRepos[index].ClearChangeTracking();

        return efCipher;
    }

    private static async Task SeedCollectionCipherAsync(
        int index,
        List<EfRepo.CollectionCipherRepository> efCollectionCipherRepos,
        Guid collectionId,
        Guid cipherId)
    {
        await efCollectionCipherRepos[index].CreateAsync(
            new CollectionCipher { CollectionId = collectionId, CipherId = cipherId });
        efCollectionCipherRepos[index].ClearChangeTracking();
    }

    private static async Task SeedSponsorshipAsync(
        int index,
        List<EfRepo.OrganizationSponsorshipRepository> efOrganizationSponsorshipRepos,
        OrganizationSponsorship sponsorship,
        Guid sponsoringOrganizationId,
        Guid sponsoringOrganizationUserId,
        Guid? sponsoredOrganizationId)
    {
        sponsorship.SponsoringOrganizationId = sponsoringOrganizationId;
        sponsorship.SponsoringOrganizationUserId = sponsoringOrganizationUserId;
        sponsorship.SponsoredOrganizationId = sponsoredOrganizationId;
        await efOrganizationSponsorshipRepos[index].CreateAsync(sponsorship);
        efOrganizationSponsorshipRepos[index].ClearChangeTracking();
    }
}
