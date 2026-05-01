using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class CollectionAccessContextServiceTests
{
    [Theory, BitAutoData]
    public async Task GetOrBuildAsync_CalledMultipleTimesForSameOrg_QueriesDbOnlyOnce(
        Guid organizationId,
        Guid userId)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.ProviderUserForOrgAsync(organizationId).Returns(false);

        var collectionRepository = Substitute.For<ICollectionRepository>();
        collectionRepository.GetManyByUserIdAsync(userId).Returns([]);

        var applicationCacheService = Substitute.For<IApplicationCacheService>();

        var organization = new CurrentContextOrganization { Id = organizationId };
        var sut = new CollectionAccessContextService();

        // Call three times — simulates Create + Update + Delete within one request
        await sut.GetOrBuildAsync(organizationId, organization, currentContext, collectionRepository, applicationCacheService);
        await sut.GetOrBuildAsync(organizationId, organization, currentContext, collectionRepository, applicationCacheService);
        await sut.GetOrBuildAsync(organizationId, organization, currentContext, collectionRepository, applicationCacheService);

        await collectionRepository.Received(1).GetManyByUserIdAsync(userId);
        await applicationCacheService.Received(1).GetOrganizationAbilityAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task GetOrBuildAsync_CalledForDifferentOrgs_QueriesDbForEachOrg(
        Guid organizationId1,
        Guid organizationId2,
        Guid userId)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        var collectionRepository = Substitute.For<ICollectionRepository>();
        collectionRepository.GetManyByUserIdAsync(userId).Returns([]);

        var applicationCacheService = Substitute.For<IApplicationCacheService>();

        var org1 = new CurrentContextOrganization { Id = organizationId1 };
        var org2 = new CurrentContextOrganization { Id = organizationId2 };
        var sut = new CollectionAccessContextService();

        await sut.GetOrBuildAsync(organizationId1, org1, currentContext, collectionRepository, applicationCacheService);
        await sut.GetOrBuildAsync(organizationId2, org2, currentContext, collectionRepository, applicationCacheService);

        await collectionRepository.Received(2).GetManyByUserIdAsync(userId);
    }
}
