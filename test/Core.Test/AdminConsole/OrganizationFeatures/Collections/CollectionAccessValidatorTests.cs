using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Collections;

[SutProviderCustomize]
public class CollectionAccessValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WithGroupsAndUsersInTheOrganization_ReturnsValid(
        Guid organizationId,
        SutProvider<CollectionAccessValidator> sutProvider)
    {
        var groups = AccessSelections(2);
        var users = AccessSelections(2);
        ArrangeGroups(sutProvider, organizationId);
        ArrangeUsers(sutProvider, organizationId);

        var result = await sutProvider.Sut.ValidateAsync(
            new CollectionAccessValidationRequest(organizationId, groups, users));

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithGroupFromDifferentOrganization_ReturnsInvalidError(
        Guid organizationId,
        SutProvider<CollectionAccessValidator> sutProvider)
    {
        ArrangeGroups(sutProvider, Guid.NewGuid());

        var result = await sutProvider.Sut.ValidateAsync(
            new CollectionAccessValidationRequest(organizationId, AccessSelections(2), null));

        Assert.True(result.IsError);
        Assert.IsType<CollectionAccessInvalidError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithNonExistentGroup_ReturnsInvalidError(
        Guid organizationId,
        SutProvider<CollectionAccessValidator> sutProvider)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var found = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(id => new Group { Id = id, OrganizationId = organizationId })
                    .ToList();
                found.RemoveAt(0);
                return found;
            });

        var result = await sutProvider.Sut.ValidateAsync(
            new CollectionAccessValidationRequest(organizationId, AccessSelections(2), null));

        Assert.True(result.IsError);
        Assert.IsType<CollectionAccessInvalidError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithUserFromDifferentOrganization_ReturnsInvalidError(
        Guid organizationId,
        SutProvider<CollectionAccessValidator> sutProvider)
    {
        ArrangeUsers(sutProvider, Guid.NewGuid());

        var result = await sutProvider.Sut.ValidateAsync(
            new CollectionAccessValidationRequest(organizationId, null, AccessSelections(2)));

        Assert.True(result.IsError);
        Assert.IsType<CollectionAccessInvalidError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithNonExistentUser_ReturnsInvalidError(
        Guid organizationId,
        SutProvider<CollectionAccessValidator> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var found = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(id => new OrganizationUser { Id = id, OrganizationId = organizationId })
                    .ToList();
                found.RemoveAt(0);
                return found;
            });

        var result = await sutProvider.Sut.ValidateAsync(
            new CollectionAccessValidationRequest(organizationId, null, AccessSelections(2)));

        Assert.True(result.IsError);
        Assert.IsType<CollectionAccessInvalidError>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithDuplicateIds_QueriesEachIdOnce(
        Guid organizationId,
        SutProvider<CollectionAccessValidator> sutProvider)
    {
        var id = Guid.NewGuid();
        var groups = new List<CollectionAccessSelection>
        {
            new() { Id = id },
            new() { Id = id },
        };
        ArrangeGroups(sutProvider, organizationId);

        var result = await sutProvider.Sut.ValidateAsync(
            new CollectionAccessValidationRequest(organizationId, groups, null));

        Assert.True(result.IsValid);
        await sutProvider.GetDependency<IGroupRepository>()
            .Received(1)
            .GetManyByManyIds(Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 1));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithNullGroupsAndUsers_DoesNotQuery(
        Guid organizationId,
        SutProvider<CollectionAccessValidator> sutProvider)
    {
        var result = await sutProvider.Sut.ValidateAsync(
            new CollectionAccessValidationRequest(organizationId, null, null));

        Assert.True(result.IsValid);
        await AssertDidNotQueryAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithEmptyGroupsAndUsers_DoesNotQuery(
        Guid organizationId,
        SutProvider<CollectionAccessValidator> sutProvider)
    {
        var result = await sutProvider.Sut.ValidateAsync(
            new CollectionAccessValidationRequest(organizationId, [], []));

        Assert.True(result.IsValid);
        await AssertDidNotQueryAsync(sutProvider);
    }

    private static List<CollectionAccessSelection> AccessSelections(int count) =>
        Enumerable.Range(0, count).Select(_ => new CollectionAccessSelection { Id = Guid.NewGuid() }).ToList();

    private static void ArrangeGroups(SutProvider<CollectionAccessValidator> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(id => new Group { Id = id, OrganizationId = organizationId })
                .ToList());
    }

    private static void ArrangeUsers(SutProvider<CollectionAccessValidator> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(id => new OrganizationUser { Id = id, OrganizationId = organizationId })
                .ToList());
    }

    private static async Task AssertDidNotQueryAsync(SutProvider<CollectionAccessValidator> sutProvider)
    {
        await sutProvider.GetDependency<IGroupRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByManyIds(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyAsync(default);
    }
}
