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
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanCreateAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(true, true)]
    [BitAutoData(false, false)]
    public async Task CanCreateAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool createNewCollections, bool limitCollectionCreationDeletion,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.LimitCollectionCreationDeletion = limitCollectionCreationDeletion;
        organization.Permissions = new Permissions
        {
            CreateNewCollections = createNewCollections
        };

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
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
    public async Task CanCreateAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        ICollection<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(true, false, false, false, true)]
    [BitAutoData(false, true, false, false, true)]
    [BitAutoData(false, false, true, false, true)]
    [BitAutoData(false, false, false, true, true)]
    [BitAutoData(false, false, false, false, false)]

    public async Task CanReadAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool manageUsers, bool editAnyCollection, bool deleteAnyCollection,
        bool createNewCollections, bool limitCollectionCreationDeletion,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.LimitCollectionCreationDeletion = limitCollectionCreationDeletion;
        organization.Permissions = new Permissions
        {
            ManageUsers = manageUsers,
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection,
            CreateNewCollections = createNewCollections
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAsync_WhenUserWithRequiredPermissions_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.LimitCollectionCreationDeletion = false;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Read },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        ICollection<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Read },
            new ClaimsPrincipal(),
            collections
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    //

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanReadAccessAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(true, false, false, true)]
    [BitAutoData(false, true, false, true)]
    [BitAutoData(false, false, true, true)]
    [BitAutoData(false, false, false, false)]

    public async Task CanReadAccessAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection,
        bool createNewCollections, bool limitCollectionCreationDeletion,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.LimitCollectionCreationDeletion = limitCollectionCreationDeletion;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection,
            CreateNewCollections = createNewCollections
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAccessAsync_WhenUserWithRequiredPermissions_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.LimitCollectionCreationDeletion = false;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadAccessAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAccessAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        ICollection<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadAccess },
            new ClaimsPrincipal(),
            collections
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    //

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanManageCollectionAccessAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions();

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Update, BulkCollectionOperations.ModifyAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

            var context = new AuthorizationHandlerContext(
                new[] { op },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.True(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_WithEditAnyCollectionPermission_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = true
        };

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Update, BulkCollectionOperations.ModifyAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

            var context = new AuthorizationHandlerContext(
                new[] { op },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.True(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_WithManageCollectionPermission_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        foreach (var c in collections)
        {
            c.Manage = true;
        }

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Update, BulkCollectionOperations.ModifyAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
            sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

            var context = new AuthorizationHandlerContext(
                new[] { op },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.True(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanManageCollectionAccessAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = false;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false
        };

        foreach (var collectionDetail in collections)
        {
            collectionDetail.Manage = true;
        }
        // Simulate one collection missing the manage permission
        collections.First().Manage = false;

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Update, BulkCollectionOperations.ModifyAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

            var context = new AuthorizationHandlerContext(
                new[] { op },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.False(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanManageCollectionAccessAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        ICollection<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        var operationsToTest = new[]
        {
            BulkCollectionOperations.Update, BulkCollectionOperations.ModifyAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

            var context = new AuthorizationHandlerContext(
                new[] { op },
                new ClaimsPrincipal(),
                collections
            );

            await sutProvider.Sut.HandleAsync(context);

            Assert.False(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanDeleteAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions();

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_WithDeleteAnyCollectionPermission_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.LimitCollectionCreationDeletion = false;
        organization.Permissions = new Permissions
        {
            DeleteAnyCollection = true
        };

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_WithManageCollectionPermission_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        foreach (var c in collections)
        {
            c.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
                new[] { BulkCollectionOperations.Delete },
                new ClaimsPrincipal(),
                collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanDeleteAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.LimitCollectionCreationDeletion = true;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false
        };

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        ICollection<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        await sutProvider.Sut.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_MissingUserId_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
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
    public async Task HandleRequirementAsync_TargetCollectionsMultipleOrgs_Exception(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        IList<Collection> collections)
    {
        var actingUserId = Guid.NewGuid();

        // Simulate a collection in a different organization
        collections.First().OrganizationId = Guid.NewGuid();

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(context));
        Assert.Equal("Requested collections must belong to the same organization.", exception.Message);
        sutProvider.GetDependency<ICurrentContext>().DidNotReceiveWithAnyArgs().GetOrganization(default);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task HandleRequirementAsync_Provider_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections)
    {
        var actingUserId = Guid.NewGuid();
        var orgId = collections.First().OrganizationId;

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Create,
            BulkCollectionOperations.Read,
            BulkCollectionOperations.ReadAccess,
            BulkCollectionOperations.Update,
            BulkCollectionOperations.ModifyAccess,
            BulkCollectionOperations.Delete,
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgId).Returns((CurrentContextOrganization)null);
            sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(true);

            var context = new AuthorizationHandlerContext(
                new[] { op },
                new ClaimsPrincipal(),
                collections
            );

            await sutProvider.Sut.HandleAsync(context);

            Assert.True(context.HasSucceeded);
            await sutProvider.GetDependency<ICurrentContext>().Received().ProviderUserForOrgAsync(orgId);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }
}
