using Bit.Core.AdminConsole.OrganizationFeatures.Groups;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
    public async Task ValidateAsync_WithSharedCollections_ReturnsNull(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        ArrangeCollections(sutProvider, organizationId);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, collectionAccess);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CollectionsDoNotExist_ReturnsCollectionNotFound(
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

        var result = await sutProvider.Sut.ValidateAsync(organizationId, collectionAccess);

        Assert.IsType<CollectionNotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CollectionsBelongToDifferentOrganization_ReturnsCollectionNotFound(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        ArrangeCollections(sutProvider, CombGuid.Generate());

        var result = await sutProvider.Sut.ValidateAsync(organizationId, collectionAccess);

        Assert.IsType<CollectionNotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithDefaultUserCollectionType_ReturnsCannotModifyDefaultUserCollection(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        ArrangeCollections(sutProvider, organizationId, CollectionType.DefaultUserCollection);

        var result = await sutProvider.Sut.ValidateAsync(organizationId, collectionAccess);

        var error = Assert.IsType<CannotModifyDefaultUserCollection>(result);
        Assert.Equal("You cannot modify group access for collections with the type as DefaultUserCollection.", error.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithOneDefaultUserCollectionAmongShared_ReturnsCannotModifyDefaultUserCollection(
        SutProvider<GroupCollectionAccessValidator> sutProvider,
        Guid organizationId, List<CollectionAccessSelection> collectionAccess)
    {
        // Only the last collection is a default collection
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var result = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(guid => new Collection { Id = guid, OrganizationId = organizationId }).ToList();
                result[^1].Type = CollectionType.DefaultUserCollection;
                return result;
            });

        var result = await sutProvider.Sut.ValidateAsync(organizationId, collectionAccess);

        var error = Assert.IsType<CannotModifyDefaultUserCollection>(result);
        Assert.Equal("You cannot modify group access for collections with the type as DefaultUserCollection.", error.Message);
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
