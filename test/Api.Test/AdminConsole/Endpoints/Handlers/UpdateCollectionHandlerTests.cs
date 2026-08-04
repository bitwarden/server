using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization.Collections;
using Bit.Api.AdminConsole.Endpoints.Handlers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.Models.Request;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Endpoints.Handlers;

[SutProviderCustomize]
public class UpdateCollectionHandlerTests
{
    private const string EncryptedName = "2.abcdEFGH123|abcdEFGH123abcdEFGH123==|abcdEFGH123abcdEFGH123abcdEFGH123abcdEFGH123abcd=";

    private static Collection MakeCollection(Guid orgId, Guid id, string? defaultUserEmail = null) =>
        new()
        {
            Id = id,
            OrganizationId = orgId,
            Name = "original-name",
            ExternalId = "original-external",
            DefaultUserCollectionEmail = defaultUserEmail
        };

    private static CollectionAccessDetails MakeAccessDetails() => new()
    {
        Users = new List<CollectionAccessSelection>(),
        Groups = new List<CollectionAccessSelection>()
    };

    private static UpdateCollectionWithDeltaRequestModel MakeModel(string? name = EncryptedName, string? externalId = "new-external") =>
        new()
        {
            Name = name,
            ExternalId = externalId,
            Users = new CollectionUserAccessDeltaRequestModel(),
            Groups = new CollectionGroupAccessDeltaRequestModel()
        };

    /// <summary>
    /// Configures the AuthorizationService so all three of the handler's checks succeed by default,
    /// letting individual tests override just the check under test.
    /// </summary>
    private static void AllowAllAuthorizationChecks(SutProvider<UpdateCollectionHandler> sutProvider)
    {
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object>(),
                Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Success());
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_CollectionNotFound_ThrowsNotFoundException(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(null, MakeAccessDetails()));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal()));

        await sutProvider.GetDependency<IUpdateCollectionCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_CollectionBelongsToDifferentOrganization_ThrowsNotFoundException(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid otherOrgId,
        Guid collectionId)
    {
        var collection = MakeCollection(otherOrgId, collectionId);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, MakeAccessDetails()));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal()));

        await sutProvider.GetDependency<IUpdateCollectionCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_MetadataUpdateAuthorizationFails_ThrowsNotFoundException(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        var collection = MakeCollection(orgId, collectionId);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));

        // The BulkCollectionOperations.Update check is evaluated against the collection itself.
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                collection,
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(BulkCollectionOperations.Update)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal()));

        await sutProvider.GetDependency<IUpdateCollectionCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default!);
        await sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_UserDeltaAuthorizationFails_ThrowsNotFoundException(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        var collection = MakeCollection(orgId, collectionId);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));

        AllowAllAuthorizationChecks(sutProvider);

        // Fail the user-access-resource check specifically.
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<CollectionUserAccessResource>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(CollectionUserOperations.Update)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal()));

        await sutProvider.GetDependency<IUpdateCollectionCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_GroupDeltaAuthorizationFails_ThrowsNotFoundException(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        var collection = MakeCollection(orgId, collectionId);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));

        AllowAllAuthorizationChecks(sutProvider);

        // Fail the group-access-resource check specifically.
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<CollectionGroupAccessResource>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(CollectionGroupOperations.Update)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal()));

        await sutProvider.GetDependency<IUpdateCollectionCommand>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!);
        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default!);
        await sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_UserDeltaCommandReturnsError_ReturnsErrorResultAndSkipsGroupDelta(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        var collection = MakeCollection(orgId, collectionId);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));
        AllowAllAuthorizationChecks(sutProvider);

        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new DuplicateOrganizationUserId()));

        var result = await sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal());

        // BadRequestError maps to a 400 with an ErrorResponseModel body — see CommandResultExtensions.
        var badRequest = Assert.IsType<BadRequest<ErrorResponseModel>>(result);
        Assert.NotNull(badRequest.Value);
        Assert.Equal(new DuplicateOrganizationUserId().Message, badRequest.Value!.Message);

        // The group delta must not run once the user delta fails — the transaction is being unwound.
        await sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>().DidNotReceiveWithAnyArgs()
            .ModifyAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_GroupDeltaCommandReturnsError_ReturnsErrorResult(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        var collection = MakeCollection(orgId, collectionId);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));
        AllowAllAuthorizationChecks(sutProvider);

        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));

        var groupError = new TestGroupBadRequestError();
        sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionGroupAccessRequest>())
            .Returns(new CommandResult(groupError));

        var result = await sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal());

        var badRequest = Assert.IsType<BadRequest<ErrorResponseModel>>(result);
        Assert.Equal(groupError.Message, badRequest.Value!.Message);

        // Metadata still ran (it's the first write inside the transaction), but the transaction
        // is not committed. We can only observe the write attempt from the mock.
        await sutProvider.GetDependency<IUpdateCollectionCommand>().Received(1)
            .UpdateAsync(collection);
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_AllThreeSucceed_ReturnsNoContent(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId,
        Guid userId,
        Guid callerOrgUserId)
    {
        var collection = MakeCollection(orgId, collectionId);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));
        AllowAllAuthorizationChecks(sutProvider);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, userId)
            .Returns(new OrganizationUser { Id = callerOrgUserId, OrganizationId = orgId, UserId = userId });

        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(orgId)
            .Returns(new OrganizationAbility { Id = orgId, AllowAdminAccessToAllCollectionItems = true });

        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));
        sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionGroupAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));

        var result = await sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal());

        Assert.IsType<NoContent>(result);
        await sutProvider.GetDependency<IUpdateCollectionCommand>().Received(1).UpdateAsync(collection);

        // The caller's OrganizationUser.Id and the org-ability flag must be threaded through to both commands.
        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().Received(1)
            .ModifyAsync(Arg.Is<ModifyCollectionUserAccessRequest>(r =>
                r.PerformingOrganizationUserId == callerOrgUserId
                && r.AllowAdminAccessToAllCollectionItems == true));
        await sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>().Received(1)
            .ModifyAsync(Arg.Is<ModifyCollectionGroupAccessRequest>(r =>
                r.PerformingOrganizationUserId == callerOrgUserId
                && r.AllowAdminAccessToAllCollectionItems == true));
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_UserNotSignedIn_PassesNullPerformingOrganizationUserId(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        var collection = MakeCollection(orgId, collectionId);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));
        AllowAllAuthorizationChecks(sutProvider);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));
        sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionGroupAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));

        var result = await sutProvider.Sut.HandleAsync(orgId, collectionId, MakeModel(), new ClaimsPrincipal());

        Assert.IsType<NoContent>(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(default, default);
        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().Received(1)
            .ModifyAsync(Arg.Is<ModifyCollectionUserAccessRequest>(r => r.PerformingOrganizationUserId == null));
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_DefaultUserCollection_DoesNotOverwriteName(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        // A default user collection has a non-null DefaultUserCollectionEmail. Its Name must not
        // be replaced by the caller's model — this mirrors the MVC UpdateCollectionRequestModel behaviour.
        var collection = MakeCollection(orgId, collectionId, defaultUserEmail: "user@example.com");
        var originalName = collection.Name;
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));
        AllowAllAuthorizationChecks(sutProvider);

        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));
        sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionGroupAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));

        var result = await sutProvider.Sut.HandleAsync(
            orgId, collectionId, MakeModel(name: EncryptedName, externalId: "new-external"), new ClaimsPrincipal());

        Assert.IsType<NoContent>(result);
        await sutProvider.GetDependency<IUpdateCollectionCommand>().Received(1)
            .UpdateAsync(Arg.Is<Collection>(c => c.Name == originalName && c.ExternalId == "new-external"));
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_NonDefaultCollection_AppliesNameFromModel(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId)
    {
        var collection = MakeCollection(orgId, collectionId, defaultUserEmail: null);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));
        AllowAllAuthorizationChecks(sutProvider);

        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));
        sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionGroupAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));

        var result = await sutProvider.Sut.HandleAsync(
            orgId, collectionId, MakeModel(name: EncryptedName, externalId: "new-external"), new ClaimsPrincipal());

        Assert.IsType<NoContent>(result);
        await sutProvider.GetDependency<IUpdateCollectionCommand>().Received(1)
            .UpdateAsync(Arg.Is<Collection>(c => c.Name == EncryptedName && c.ExternalId == "new-external"));
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_MapsDeltaSelectionsThroughToCommands(
        SutProvider<UpdateCollectionHandler> sutProvider,
        Guid orgId,
        Guid collectionId,
        Guid addUserId,
        Guid updateUserId,
        Guid removeUserId,
        Guid addGroupId,
        Guid removeGroupId)
    {
        var collection = MakeCollection(orgId, collectionId);
        var accessDetails = MakeAccessDetails();

        sutProvider.GetDependency<ICollectionRepository>()
            .GetByIdWithAccessAsync(collectionId)
            .Returns(new Tuple<Collection?, CollectionAccessDetails>(collection, accessDetails));
        AllowAllAuthorizationChecks(sutProvider);

        sutProvider.GetDependency<IModifyCollectionUserAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionUserAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));
        sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>()
            .ModifyAsync(Arg.Any<ModifyCollectionGroupAccessRequest>())
            .Returns(new CommandResult(new OneOf.Types.None()));

        var model = new UpdateCollectionWithDeltaRequestModel
        {
            Name = EncryptedName,
            ExternalId = "ext",
            Users = new CollectionUserAccessDeltaRequestModel
            {
                Add = new[] { new SelectionReadOnlyRequestModel { Id = addUserId, Manage = true } },
                Update = new[] { new SelectionReadOnlyRequestModel { Id = updateUserId, ReadOnly = true } },
                Remove = new[] { removeUserId }
            },
            Groups = new CollectionGroupAccessDeltaRequestModel
            {
                Add = new[] { new SelectionReadOnlyRequestModel { Id = addGroupId, Manage = true } },
                Remove = new[] { removeGroupId }
            }
        };

        var result = await sutProvider.Sut.HandleAsync(orgId, collectionId, model, new ClaimsPrincipal());

        Assert.IsType<NoContent>(result);
        await sutProvider.GetDependency<IModifyCollectionUserAccessCommand>().Received(1)
            .ModifyAsync(Arg.Is<ModifyCollectionUserAccessRequest>(r =>
                r.Add.Any(s => s.Id == addUserId && s.Manage)
                && r.Update.Any(s => s.Id == updateUserId && s.ReadOnly)
                && r.Remove.Contains(removeUserId)));
        await sutProvider.GetDependency<IModifyCollectionGroupAccessCommand>().Received(1)
            .ModifyAsync(Arg.Is<ModifyCollectionGroupAccessRequest>(r =>
                r.Add.Any(s => s.Id == addGroupId && s.Manage)
                && r.Remove.Contains(removeGroupId)));
    }

    /// <summary>
    /// Local test-only error used to drive the group-command failure path without pulling in a
    /// production error record whose message might change.
    /// </summary>
    private record TestGroupBadRequestError() : BadRequestError("Group delta rejected.");
}
