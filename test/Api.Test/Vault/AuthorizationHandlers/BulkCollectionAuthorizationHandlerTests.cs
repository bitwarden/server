using System.Security.Claims;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
[FeatureServiceCustomize(FeatureFlagKeys.FlexibleCollections)]
public class BulkCollectionAuthorizationHandlerTests
{
    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User, false, false)]
    [BitAutoData(OrganizationUserType.Admin, false, true)]
    [BitAutoData(OrganizationUserType.Owner, false, true)]
    [BitAutoData(OrganizationUserType.Custom, true, true)]
    public async Task CanCreateAsync_Success(
        OrganizationUserType userType, bool createNewCollection, bool limitCollectionCreateDelete,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            CreateNewCollections = createNewCollection
        };
        organization.LimitCollectionCreationDeletion = limitCollectionCreateDelete;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_WhenProviderUser_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions
        {
            CreateNewCollections = false
        };
        organization.LimitCollectionCreationDeletion = true;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanCreateAsync_WhenMissingCreateCollectionPermission_Failure(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            CreateNewCollections = false
        };
        organization.LimitCollectionCreationDeletion = true;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAsync_WhenProviderUser_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    public async Task CanReadAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAsync_WhenUserAssigned_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAsync_WhenMissingAccess_Failure(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadAccessAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAccessAsync_WhenProviderUser_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    public async Task CanReadAccessAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAccessAsync_WhenUserAssigned_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAccessAsync_WhenMissingAccess_Failure(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User, false, true)]
    [BitAutoData(OrganizationUserType.Admin, false, false)]
    [BitAutoData(OrganizationUserType.Owner, false, false)]
    [BitAutoData(OrganizationUserType.Custom, true, false)]
    [BitAutoData(OrganizationUserType.Owner, true, true)]
    public async Task CanUpdateAsync_Success(
        OrganizationUserType userType, bool editAnyCollection, bool manageCollections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionDetails> collectionDetails,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = manageCollections;
        }

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Update },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdateAsync_WhenProviderUser_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Update },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdateAsync_WhenMissingManageCollectionPermission_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionDetails> collectionDetails,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }
        // Simulate one collection missing the manage permission
        collectionDetails.First().Manage = false;

        // Ensure the user is not an owner/admin and does not have edit any collection permission
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Update },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
        sutProvider.GetDependency<ICurrentContext>().ReceivedWithAnyArgs().GetOrganization(default);
        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs()
            .GetManyByUserIdAsync(default);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User, false, true)]
    [BitAutoData(OrganizationUserType.Admin, false, false)]
    [BitAutoData(OrganizationUserType.Owner, false, false)]
    [BitAutoData(OrganizationUserType.Custom, true, false)]
    [BitAutoData(OrganizationUserType.Owner, true, true)]
    public async Task CanManageCollectionAccessAsync_Success(
        OrganizationUserType userType, bool editAnyCollection, bool manageCollections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionDetails> collectionDetails,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = manageCollections;
        }

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ModifyAccess },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_WhenProviderUser_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ModifyAccess },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_WhenMissingManageCollectionPermission_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionDetails> collectionDetails,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = true;
        }
        // Simulate one collection missing the manage permission
        collectionDetails.First().Manage = false;

        // Ensure the user is not an owner/admin and does not have edit any collection permission
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.ModifyAccess },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
        sutProvider.GetDependency<ICurrentContext>().ReceivedWithAnyArgs().GetOrganization(default);
        await sutProvider.GetDependency<ICollectionRepository>().ReceivedWithAnyArgs()
            .GetManyByUserIdAsync(default);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User, false, false, true)]
    [BitAutoData(OrganizationUserType.Admin, false, true, false)]
    [BitAutoData(OrganizationUserType.Owner, false, true, false)]
    [BitAutoData(OrganizationUserType.Custom, true, true, false)]
    public async Task CanDeleteAsync_Success(
        OrganizationUserType userType, bool deleteAnyCollection, bool limitCollectionCreateDelete, bool manageCollections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        ICollection<CollectionDetails> collectionDetails,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();
        foreach (var collectionDetail in collectionDetails)
        {
            collectionDetail.Manage = manageCollections;
        }

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            DeleteAnyCollection = deleteAnyCollection
        };
        organization.LimitCollectionCreationDeletion = limitCollectionCreateDelete;

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collectionDetails);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_WithProviderUser_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions
        {
            DeleteAnyCollection = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(true);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_WhenCustomUserWithRequiredPermissions_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            DeleteAnyCollection = true
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanDeleteAsync_WhenMissingPermissions_Failure(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions
        {
            DeleteAnyCollection = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_NullResource_NoSuccessOrFailure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            null
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_EmptyResourceList_NoSuccessOrFailure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            new List<Collection>()
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(new Guid());

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        // Simulate missing user id
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
        sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs();
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_TargetCollectionsMultipleOrgs_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        IList<Collection> collections)
    {
        var actingUserId = Guid.NewGuid();

        // Simulate a collection in a different organization
        collections.First().OrganizationId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(context));
        Assert.Equal("Requested collections must belong to the same organization.", exception.Message);
        sutProvider.GetDependency<ICurrentContext>().DidNotReceiveWithAnyArgs().GetOrganization(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MissingOrg_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        var actingUserId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { CollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.True(context.HasFailed);
    }
}
