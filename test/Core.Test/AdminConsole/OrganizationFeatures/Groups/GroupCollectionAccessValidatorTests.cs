using Bit.Core.AdminConsole.OrganizationFeatures.Groups;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Groups;

[SutProviderCustomize]
public class GroupCollectionAccessValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WithSharedCollections_Succeeds(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        ArrangeCollections(sutProvider, organizationId);

        await sutProvider.Sut.ValidateAsync(organizationId, collectionAccess);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CollectionsDoNotExist_Throws(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        // Return result is missing a collection
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var result = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(guid => new Collection { Id = guid, OrganizationId = organizationId }).ToList();
                result.RemoveAt(0);
                return result;
            });

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.ValidateAsync(organizationId, collectionAccess));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CollectionsBelongToDifferentOrganization_Throws(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        ArrangeCollections(sutProvider, CoreHelpers.GenerateComb());

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.ValidateAsync(organizationId, collectionAccess));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithDefaultUserCollectionType_Throws(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        ArrangeCollections(sutProvider, organizationId, CollectionType.DefaultUserCollection);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(organizationId, collectionAccess));
        Assert.Contains("You cannot modify group access for collections with the type as DefaultUserCollection.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithOneDefaultUserCollectionAmongShared_Throws(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        // Only the last collection is a default collection, so the check cannot rely on the first one
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var result = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(guid => new Collection { Id = guid, OrganizationId = organizationId }).ToList();
                result[^1].Type = CollectionType.DefaultUserCollection;
                return result;
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(organizationId, collectionAccess));
        Assert.Contains("You cannot modify group access for collections with the type as DefaultUserCollection.", exception.Message);
    }

    private static void ArrangeCollections(SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, CollectionType type = CollectionType.SharedCollection)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Collection { Id = guid, OrganizationId = organizationId, Type = type }).ToList());
    }
}
