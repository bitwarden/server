using System.Security.Claims;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.AuthorizationHandlers;

[SutProviderCustomize]
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
        organization.Permissions = new Permissions();

        ArrangeOrganizationAbility(sutProvider, organization, true, true);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_WhenUser_WithLimitCollectionCreationFalse_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;

        ArrangeOrganizationAbility(sutProvider, organization, false, false);

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
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false
        };

        ArrangeOrganizationAbility(sutProvider, organization, true, true);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanCreateAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        CurrentContextOrganization organization,
        List<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        collections.ForEach(c => c.OrganizationId = organization.Id);
        ArrangeOrganizationAbility(sutProvider, organization, true, true);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Create },
            new ClaimsPrincipal(),
            collections
        );

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

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
        organization.Permissions = new Permissions();

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Read, BulkCollectionOperations.ReadAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

            var context = new AuthorizationHandlerContext(
                new[] { BulkCollectionOperations.Read },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.True(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, CollectionCustomization]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    public async Task CanReadAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection
        };

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Read, BulkCollectionOperations.ReadAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

            var context = new AuthorizationHandlerContext(
                new[] { BulkCollectionOperations.Read },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.True(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAsync_WhenUserCanManageCollections_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        foreach (var c in collections)
        {
            c.Manage = true;
        }

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Read, BulkCollectionOperations.ReadAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
            sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

            var context = new AuthorizationHandlerContext(
                new[] { BulkCollectionOperations.Read },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.True(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAsync_WhenUserIsNotAssignedToCollections_NoSuccess(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Read, BulkCollectionOperations.ReadAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

            var context = new AuthorizationHandlerContext(
                new[] { BulkCollectionOperations.Read },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.False(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
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
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false
        };

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Read, BulkCollectionOperations.ReadAccess
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

            var context = new AuthorizationHandlerContext(
                new[] { BulkCollectionOperations.Read },
                new ClaimsPrincipal(),
                collections);

            await sutProvider.Sut.HandleAsync(context);

            Assert.False(context.HasSucceeded);

            // Recreate the SUT to reset the mocks/dependencies between tests
            sutProvider.Recreate();
        }
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        ICollection<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        var operationsToTest = new[]
        {
            BulkCollectionOperations.Read, BulkCollectionOperations.ReadAccess
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
    public async Task CanReadWithAccessAsync_WhenAdminOrOwner_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadWithAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(false, false, true)]

    public async Task CanReadWithAccessAsync_WhenCustomUserWithRequiredPermissions_Success(
        bool editAnyCollection, bool deleteAnyCollection, bool manageUsers,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            EditAnyCollection = editAnyCollection,
            DeleteAnyCollection = deleteAnyCollection,
            ManageUsers = manageUsers
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadWithAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadWithAccessAsync_WhenUserCanManageCollections_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        foreach (var c in collections)
        {
            c.Manage = true;
        }

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadWithAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadWithAccessAsync_WhenUserCanNotManageCollections_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        foreach (var c in collections)
        {
            c.Manage = false;
        }

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadWithAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CanReadWithAccessAsync_WhenMissingPermissions_NoSuccess(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
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
            new[] { BulkCollectionOperations.ReadWithAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanReadWithAccessAsync_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        ICollection<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ReadWithAccess },
            new ClaimsPrincipal(),
            collections
        );

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanUpdateCollection_WhenAdminOrOwner_WithV1Enabled_PermittedByCollectionManagementSettings_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections, CurrentContextOrganization organization,
        OrganizationAbility organizationAbility)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();
        organizationAbility.Id = organization.Id;
        organizationAbility.AllowAdminAccessToAllCollectionItems = true;

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Update,
            BulkCollectionOperations.ModifyUserAccess,
            BulkCollectionOperations.ModifyGroupAccess,
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
            sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id)
                .Returns(organizationAbility);

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
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanUpdateCollection_WhenAdminOrOwner_WithV1Enabled_NotPermittedByCollectionManagementSettings_Failure(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections, CurrentContextOrganization organization,
        OrganizationAbility organizationAbility)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();
        organizationAbility.Id = organization.Id;
        organizationAbility.AllowAdminAccessToAllCollectionItems = false;

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Update,
            BulkCollectionOperations.ModifyUserAccess,
            BulkCollectionOperations.ModifyGroupAccess,
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
            sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id)
                .Returns(organizationAbility);

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
    public async Task CanUpdateCollection_WithEditAnyCollectionPermission_Success(
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
            BulkCollectionOperations.Update,
            BulkCollectionOperations.ModifyUserAccess,
            BulkCollectionOperations.ModifyGroupAccess,
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
    public async Task CanUpdateCollection_WithManageCollectionPermission_Success(
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
            BulkCollectionOperations.Update,
            BulkCollectionOperations.ModifyUserAccess,
            BulkCollectionOperations.ModifyGroupAccess,
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
    public async Task CanUpdateCollection_WhenMissingPermissions_NoSuccess(
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
            BulkCollectionOperations.Update,
            BulkCollectionOperations.ModifyUserAccess,
            BulkCollectionOperations.ModifyGroupAccess,
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
    public async Task CanUpdateCollection_WhenMissingOrgAccess_NoSuccess(
        Guid userId,
        ICollection<Collection> collections,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider)
    {
        var operationsToTest = new[]
        {
            BulkCollectionOperations.Update,
            BulkCollectionOperations.ModifyUserAccess,
            BulkCollectionOperations.ModifyGroupAccess,
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

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdateUsers_WithManageUsersCustomPermission_AllowAdminAccessIsTrue_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider, ICollection<Collection> collections,
        CurrentContextOrganization organization, Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            ManageUsers = true
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ModifyUserAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdateUsers_WithManageUsersCustomPermission_AllowAdminAccessIsFalse_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider, ICollection<Collection> collections,
        CurrentContextOrganization organization, Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            ManageUsers = true
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ModifyUserAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdateGroups_WithManageGroupsCustomPermission_AllowAdminAccessIsTrue_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider, ICollection<Collection> collections,
        CurrentContextOrganization organization, Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            ManageGroups = true
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = true });

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ModifyGroupAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanUpdateGroups_WithManageGroupsCustomPermission_AllowAdminAccessIsFalse_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider, ICollection<Collection> collections,
        CurrentContextOrganization organization, Guid actingUserId)
    {
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            ManageGroups = true
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { AllowAdminAccessToAllCollectionItems = false });

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.ModifyGroupAccess },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_WithDeleteAnyCollectionPermission_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.Custom;
        organization.Permissions = new Permissions
        {
            DeleteAnyCollection = true
        };

        ArrangeOrganizationAbility(sutProvider, organization, true, true);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CollectionCustomization]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanDeleteAsync_WhenAdminOrOwner_AllowAdminAccessToAllCollectionItemsTrue_Success(
        OrganizationUserType userType,
        Guid userId, SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<Collection> collections,
        CurrentContextOrganization organization)
    {
        organization.Type = userType;
        organization.Permissions = new Permissions();

        ArrangeOrganizationAbility(sutProvider, organization, true, true);

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
    public async Task CanDeleteAsync_WhenUser_LimitCollectionDeletionFalse_WithCanManagePermission_Success(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        ArrangeOrganizationAbility(sutProvider, organization, false, false);

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
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.User)]
    public async Task CanDeleteAsync_LimitCollectionDeletionFalse_AllowAdminAccessToAllCollectionItemsFalse_WithCanManagePermission_Success(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions();

        ArrangeOrganizationAbility(sutProvider, organization, false, false, false);

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
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanDeleteAsync_WhenAdminOrOwner_LimitCollectionDeletionTrue_AllowAdminAccessToAllCollectionItemsFalse_WithCanManagePermission_Success(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions();

        ArrangeOrganizationAbility(sutProvider, organization, true, true, false);

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
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CanDeleteAsync_WhenAdminOrOwner_LimitCollectionDeletionTrue_AllowAdminAccessToAllCollectionItemsFalse_WithoutCanManagePermission_Failure(
        OrganizationUserType userType,
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = userType;
        organization.Permissions = new Permissions();

        ArrangeOrganizationAbility(sutProvider, organization, true, true, false);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        foreach (var c in collections)
        {
            c.Manage = false;
        }

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_WhenUser_LimitCollectionDeletionTrue_AllowAdminAccessToAllCollectionItemsTrue_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        ArrangeOrganizationAbility(sutProvider, organization, true, true);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        foreach (var c in collections)
        {
            c.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CanDeleteAsync_WhenUser_LimitCollectionDeletionTrue_AllowAdminAccessToAllCollectionItemsFalse_Failure(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        ICollection<CollectionDetails> collections,
        CurrentContextOrganization organization)
    {
        var actingUserId = Guid.NewGuid();

        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        ArrangeOrganizationAbility(sutProvider, organization, true, true, false);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>().GetManyByUserIdAsync(actingUserId).Returns(collections);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

        foreach (var c in collections)
        {
            c.Manage = true;
        }

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
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
        organization.Permissions = new Permissions
        {
            EditAnyCollection = false,
            DeleteAnyCollection = false,
            ManageGroups = false,
            ManageUsers = false
        };

        ArrangeOrganizationAbility(sutProvider, organization, true, true);

        var context = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Delete },
            new ClaimsPrincipal(),
            collections);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

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
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(Arg.Any<Guid>()).Returns(false);

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

        var organizationAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            { collections.First().OrganizationId,
                new OrganizationAbility
                {
                    LimitCollectionCreation = true,
                    LimitCollectionDeletion = true,
                    LimitCollectionCreationDeletion = true,
                    AllowAdminAccessToAllCollectionItems = true
                }
            }
        };

        var operationsToTest = new[]
        {
            BulkCollectionOperations.Create,
            BulkCollectionOperations.Read,
            BulkCollectionOperations.ReadAccess,
            BulkCollectionOperations.Update,
            BulkCollectionOperations.ModifyUserAccess,
            BulkCollectionOperations.ModifyGroupAccess,
            BulkCollectionOperations.Delete,
        };

        foreach (var op in operationsToTest)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
            sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgId).Returns((CurrentContextOrganization)null);
            sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync()
                .Returns(organizationAbilities);
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

    [Theory, BitAutoData, CollectionCustomization]
    public async Task CachesCollectionsWithCanManagePermissions(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        CollectionDetails collection1, CollectionDetails collection2,
        CurrentContextOrganization organization, Guid actingUserId)
    {
        organization.Type = OrganizationUserType.User;
        organization.Permissions = new Permissions();

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByUserIdAsync(actingUserId)
            .Returns(new List<CollectionDetails>() { collection1, collection2 });

        var context1 = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Update },
            new ClaimsPrincipal(),
            collection1);

        await sutProvider.Sut.HandleAsync(context1);

        var context2 = new AuthorizationHandlerContext(
            new[] { BulkCollectionOperations.Update },
            new ClaimsPrincipal(),
            collection2);

        await sutProvider.Sut.HandleAsync(context2);

        // Expect: only calls the database once
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).GetManyByUserIdAsync(Arg.Any<Guid>());
    }

    private static void ArrangeOrganizationAbility(
        SutProvider<BulkCollectionAuthorizationHandler> sutProvider,
        CurrentContextOrganization organization,
        bool limitCollectionCreation,
        bool limitCollectionDeletion,
        bool allowAdminAccessToAllCollectionItems = true)
    {
        var organizationAbility = new OrganizationAbility();
        organizationAbility.Id = organization.Id;
        organizationAbility.LimitCollectionCreation = limitCollectionCreation;
        organizationAbility.LimitCollectionDeletion = limitCollectionDeletion;
        // Deprecated: remove with https://bitwarden.atlassian.net/browse/PM-10863
        organizationAbility.LimitCollectionCreationDeletion = limitCollectionCreation || limitCollectionDeletion;
        organizationAbility.AllowAdminAccessToAllCollectionItems = allowAdminAccessToAllCollectionItems;


        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);
    }
}
