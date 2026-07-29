using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Endpoints.Handlers;
using Bit.Api.Models.Request;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using OneOf.Types;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Endpoints.Handlers;

[SutProviderCustomize]
public class CollectionUserEndpointsHandlerTests
{
    [Theory, BitAutoData]
    public async Task PatchUserAccessAsync_CollectionNotFound_ThrowsNotFound(
        Guid orgId, Guid collectionId, SutProvider<CollectionUserEndpointsHandler> sutProvider)
    {
        ArrangeCollections(sutProvider, orgId);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PatchUserAccessAsync(
            orgId, [collectionId], [], [], [], new ClaimsPrincipal()));

        await sutProvider.GetDependency<IAuthorizationService>().DidNotReceiveWithAnyArgs()
            .AuthorizeAsync(default, default, default(IEnumerable<IAuthorizationRequirement>));
    }

    [Theory, BitAutoData]
    public async Task PatchUserAccessAsync_SomeCollectionIdsNotFound_ThrowsNotFound(
        Guid orgId, SutProvider<CollectionUserEndpointsHandler> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PatchUserAccessAsync(
            orgId, [collectionA.Id, Guid.NewGuid()], [], [], [], new ClaimsPrincipal()));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PatchUserAccessAsync_EmptyDelta_StillAuthorizesUpdate(
        Guid orgId, SutProvider<CollectionUserEndpointsHandler> sutProvider)
    {
        var collection = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collection);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Update);
        ArrangeCommandSucceeds(sutProvider);

        await sutProvider.Sut.PatchUserAccessAsync(orgId, [collection.Id], [], [], [], new ClaimsPrincipal());

        await sutProvider.GetDependency<IAuthorizationService>().Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(CollectionUserOperations.Update)));
    }

    [Theory, BitAutoData]
    public async Task PatchUserAccessAsync_Authorized_AppliesDeltaToSingleTarget(
        Guid orgId, Guid newUserId, Guid updatedUserId, Guid removedUserId,
        SutProvider<CollectionUserEndpointsHandler> sutProvider)
    {
        var collection = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collection);
        ArrangeAuthorization(sutProvider,
            CollectionUserOperations.Create, CollectionUserOperations.Update, CollectionUserOperations.Delete);
        ArrangeCommandSucceeds(sutProvider);

        var add = new List<SelectionReadOnlyRequestModel> { new() { Id = newUserId, Manage = true } };
        var update = new List<SelectionReadOnlyRequestModel> { new() { Id = updatedUserId } };
        var remove = new List<Guid> { removedUserId };

        await sutProvider.Sut.PatchUserAccessAsync(orgId, [collection.Id], add, update, remove, new ClaimsPrincipal());

        await sutProvider.GetDependency<IAuthorizationService>().Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(CollectionUserOperations.Create)));
        await sutProvider.GetDependency<IAuthorizationService>().Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(CollectionUserOperations.Update)));
        await sutProvider.GetDependency<IAuthorizationService>().Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(CollectionUserOperations.Delete)));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().Received(1).ModifyAsync(
            Arg.Is<ModifyCollectionUserAccessRequest>(r =>
                r.Targets.Single().Collection.Id == collection.Id &&
                r.Add.Single().Id == newUserId && r.Add.Single().Manage &&
                r.Update.Single().Id == updatedUserId &&
                r.Remove.Single() == removedUserId));
    }

    [Theory, BitAutoData]
    public async Task PatchUserAccessAsync_UnauthorizedForCreate_ThrowsAndNeverCallsCommand(
        Guid orgId, Guid newUserId, SutProvider<CollectionUserEndpointsHandler> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA, collectionB);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Update, CollectionUserOperations.Delete);

        var add = new List<SelectionReadOnlyRequestModel> { new() { Id = newUserId } };

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PatchUserAccessAsync(
            orgId, [collectionA.Id, collectionB.Id], add, [], [], new ClaimsPrincipal()));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PatchUserAccessAsync_UnauthorizedForUpdate_ThrowsAndNeverCallsCommand(
        Guid orgId, Guid updatedUserId, SutProvider<CollectionUserEndpointsHandler> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA, collectionB);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Create, CollectionUserOperations.Delete);

        var update = new List<SelectionReadOnlyRequestModel> { new() { Id = updatedUserId } };

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PatchUserAccessAsync(
            orgId, [collectionA.Id, collectionB.Id], [], update, [], new ClaimsPrincipal()));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PatchUserAccessAsync_UnauthorizedForDelete_ThrowsAndNeverCallsCommand(
        Guid orgId, Guid removedUserId, SutProvider<CollectionUserEndpointsHandler> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA, collectionB);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Create, CollectionUserOperations.Update);

        var remove = new List<Guid> { removedUserId };

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PatchUserAccessAsync(
            orgId, [collectionA.Id, collectionB.Id], [], [], remove, new ClaimsPrincipal()));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PatchUserAccessAsync_Authorized_AppliesSameDeltaToAllTargets(
        Guid orgId, Guid newUserId, SutProvider<CollectionUserEndpointsHandler> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA, collectionB);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Create);
        ArrangeCommandSucceeds(sutProvider);

        var add = new List<SelectionReadOnlyRequestModel> { new() { Id = newUserId, Manage = true } };

        await sutProvider.Sut.PatchUserAccessAsync(
            orgId, [collectionA.Id, collectionB.Id], add, [], [], new ClaimsPrincipal());

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().Received(1).ModifyAsync(
            Arg.Is<ModifyCollectionUserAccessRequest>(r =>
                r.Targets.Select(t => t.Collection.Id).ToHashSet().SetEquals(new[] { collectionA.Id, collectionB.Id }) &&
                r.Add.Single().Id == newUserId && r.Add.Single().Manage));
    }

    private static void ArrangeCollections(
        SutProvider<CollectionUserEndpointsHandler> sutProvider, Guid organizationId, params Collection[] collections)
    {
        var repository = sutProvider.GetDependency<ICollectionRepository>();

        repository.GetManyByOrganizationIdWithAccessAsync(organizationId)
            .Returns(collections
                .Select(c => new Tuple<Collection, CollectionAccessDetails>(
                    c, new CollectionAccessDetails { Users = [], Groups = [] }))
                .ToList());

        // The single-collection route resolves via GetByIdWithAccessAsync instead. Default to "not found"
        // for any id, then override with the real collection for each one actually arranged.
        repository.GetByIdWithAccessAsync(Arg.Any<Guid>())
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(null, new CollectionAccessDetails { Users = [], Groups = [] }));
        foreach (var collection in collections)
        {
            repository.GetByIdWithAccessAsync(collection.Id)
                .Returns(new Tuple<Collection?, CollectionAccessDetails>(
                    collection, new CollectionAccessDetails { Users = [], Groups = [] }));
        }
    }

    // Only the given operations succeed authorization; any other requirement fails. This lets tests prove
    // *which* operation the handler actually checked, instead of a blanket succeed/fail for every requirement.
    private static void ArrangeAuthorization(
        SutProvider<CollectionUserEndpointsHandler> sutProvider, params CollectionUserOperationRequirement[] succeedingOperations) =>
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(callInfo =>
            {
                var requirements = callInfo.Arg<IEnumerable<IAuthorizationRequirement>>();
                return requirements.All(succeedingOperations.Contains)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed();
            });

    private static void ArrangeCommandSucceeds(SutProvider<CollectionUserEndpointsHandler> sutProvider) =>
        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new None()));
}
