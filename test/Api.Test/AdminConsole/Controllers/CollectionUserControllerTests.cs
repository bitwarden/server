using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request;
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

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(CollectionUserController))]
[SutProviderCustomize]
public class CollectionUserControllerTests
{
    [Theory, BitAutoData]
    public async Task PatchCollectionUserAccessAsync_CollectionNotFound_ThrowsNotFound(
        Guid orgId, Guid collectionId, SutProvider<CollectionUserController> sutProvider)
    {
        ArrangeCollections(sutProvider, orgId);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PatchCollectionUserAccessAsync(
            orgId, collectionId, new CollectionUserAccessDeltaRequestModel()));

        await sutProvider.GetDependency<IAuthorizationService>().DidNotReceiveWithAnyArgs()
            .AuthorizeAsync(default, default, default(IEnumerable<IAuthorizationRequirement>));
    }

    [Theory, BitAutoData]
    public async Task PatchBulkCollectionUserAccessAsync_SomeCollectionIdsNotFound_ThrowsNotFound(
        Guid orgId, SutProvider<CollectionUserController> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA);

        var model = new BulkCollectionUserAccessDeltaRequestModel
        {
            CollectionIds = [collectionA.Id, Guid.NewGuid()]
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PatchBulkCollectionUserAccessAsync(orgId, model));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PatchCollectionUserAccessAsync_EmptyDelta_StillAuthorizesUpdate(
        Guid orgId, SutProvider<CollectionUserController> sutProvider)
    {
        var collection = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collection);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Update);
        ArrangeCommandSucceeds(sutProvider);

        await sutProvider.Sut.PatchCollectionUserAccessAsync(orgId, collection.Id, new CollectionUserAccessDeltaRequestModel());

        await sutProvider.GetDependency<IAuthorizationService>().Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(CollectionUserOperations.Update)));
    }

    [Theory, BitAutoData]
    public async Task PatchCollectionUserAccessAsync_Authorized_AppliesDeltaToSingleTarget(
        Guid orgId, Guid newUserId, Guid updatedUserId, Guid removedUserId,
        SutProvider<CollectionUserController> sutProvider)
    {
        var collection = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collection);
        ArrangeAuthorization(sutProvider,
            CollectionUserOperations.Create, CollectionUserOperations.Update, CollectionUserOperations.Delete);
        ArrangeCommandSucceeds(sutProvider);

        var model = new CollectionUserAccessDeltaRequestModel
        {
            Add = [new SelectionReadOnlyRequestModel { Id = newUserId, Manage = true }],
            Update = [new SelectionReadOnlyRequestModel { Id = updatedUserId }],
            Remove = [removedUserId]
        };

        await sutProvider.Sut.PatchCollectionUserAccessAsync(orgId, collection.Id, model);

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
    public async Task PatchBulkCollectionUserAccessAsync_UnauthorizedForCreate_ThrowsAndNeverCallsCommand(
        Guid orgId, Guid newUserId, SutProvider<CollectionUserController> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA, collectionB);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Update, CollectionUserOperations.Delete);

        var model = new BulkCollectionUserAccessDeltaRequestModel
        {
            CollectionIds = [collectionA.Id, collectionB.Id],
            Add = [new SelectionReadOnlyRequestModel { Id = newUserId }]
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PatchBulkCollectionUserAccessAsync(orgId, model));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PatchBulkCollectionUserAccessAsync_UnauthorizedForUpdate_ThrowsAndNeverCallsCommand(
        Guid orgId, Guid updatedUserId, SutProvider<CollectionUserController> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA, collectionB);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Create, CollectionUserOperations.Delete);

        var model = new BulkCollectionUserAccessDeltaRequestModel
        {
            CollectionIds = [collectionA.Id, collectionB.Id],
            Update = [new SelectionReadOnlyRequestModel { Id = updatedUserId }]
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PatchBulkCollectionUserAccessAsync(orgId, model));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PatchBulkCollectionUserAccessAsync_UnauthorizedForDelete_ThrowsAndNeverCallsCommand(
        Guid orgId, Guid removedUserId, SutProvider<CollectionUserController> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA, collectionB);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Create, CollectionUserOperations.Update);

        var model = new BulkCollectionUserAccessDeltaRequestModel
        {
            CollectionIds = [collectionA.Id, collectionB.Id],
            Remove = [removedUserId]
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PatchBulkCollectionUserAccessAsync(orgId, model));

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PatchBulkCollectionUserAccessAsync_Authorized_AppliesSameDeltaToAllTargets(
        Guid orgId, Guid newUserId, SutProvider<CollectionUserController> sutProvider)
    {
        var collectionA = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        var collectionB = new Collection { Id = Guid.NewGuid(), OrganizationId = orgId };
        ArrangeCollections(sutProvider, orgId, collectionA, collectionB);
        ArrangeAuthorization(sutProvider, CollectionUserOperations.Create);
        ArrangeCommandSucceeds(sutProvider);

        var model = new BulkCollectionUserAccessDeltaRequestModel
        {
            CollectionIds = [collectionA.Id, collectionB.Id],
            Add = [new SelectionReadOnlyRequestModel { Id = newUserId, Manage = true }]
        };

        await sutProvider.Sut.PatchBulkCollectionUserAccessAsync(orgId, model);

        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().Received(1).ModifyAsync(
            Arg.Is<ModifyCollectionUserAccessRequest>(r =>
                r.Targets.Select(t => t.Collection.Id).ToHashSet().SetEquals(new[] { collectionA.Id, collectionB.Id }) &&
                r.Add.Single().Id == newUserId && r.Add.Single().Manage));
    }

    private static void ArrangeCollections(
        SutProvider<CollectionUserController> sutProvider, Guid organizationId, params Collection[] collections)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdWithAccessAsync(organizationId)
            .Returns(collections
                .Select(c => new Tuple<Collection, CollectionAccessDetails>(
                    c, new CollectionAccessDetails { Users = [], Groups = [] }))
                .ToList());
    }

    // Only the given operations succeed authorization; any other requirement fails. This lets tests prove
    // *which* operation the controller actually checked, instead of a blanket succeed/fail for every requirement.
    private static void ArrangeAuthorization(
        SutProvider<CollectionUserController> sutProvider, params CollectionUserOperationRequirement[] succeedingOperations) =>
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(callInfo =>
            {
                var requirements = callInfo.Arg<IEnumerable<IAuthorizationRequirement>>();
                return requirements.All(succeedingOperations.Contains)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed();
            });

    private static void ArrangeCommandSucceeds(SutProvider<CollectionUserController> sutProvider) =>
        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new None()));
}
