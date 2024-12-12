using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Authorization;

[SutProviderCustomize]
public class SecurityTaskAuthorizationHandlerTests
{
    [Theory, CurrentContextOrganizationCustomize, BitAutoData]
    public async Task MissingOrg_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns((CurrentContextOrganization)null);

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Read },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize, BitAutoData]
    public async Task MissingCipherId_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var operations = new[]
        {
            SecurityTaskOperations.Read, SecurityTaskOperations.Create, SecurityTaskOperations.Update
        };
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = null
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        foreach (var operation in operations)
        {
            var context = new AuthorizationHandlerContext(
                new[] { operation },
                new ClaimsPrincipal(),
                task);

            await sutProvider.Sut.HandleAsync(context);

            Assert.False(context.HasSucceeded, operation.ToString());
        }

    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.User), BitAutoData]
    public async Task Read_User_CanReadCipher_Success(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };
        var cipherPermissions = new OrganizationCipherPermission
        {
            Id = task.CipherId.Value,
            Read = true
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>
        {
            { task.CipherId.Value, cipherPermissions }
        });

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Read },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Admin), BitAutoData]
    public async Task Read_Admin_Success(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };
        var cipherPermissions = new OrganizationCipherPermission
        {
            Id = task.CipherId.Value,
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>
        {
            { task.CipherId.Value, cipherPermissions }
        });

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Read },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Admin), BitAutoData]
    public async Task Read_Admin_MissingCipher_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>());

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Read },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.User), BitAutoData]
    public async Task Read_User_CannotReadCipher_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };
        var cipherPermissions = new OrganizationCipherPermission
        {
            Id = task.CipherId.Value,
            Read = false
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>
        {
            { task.CipherId.Value, cipherPermissions }
        });

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Read },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.User), BitAutoData]
    public async Task Create_User_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };
        var cipherPermissions = new OrganizationCipherPermission
        {
            Id = task.CipherId.Value,
            Read = true,
            Edit = true,
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>
        {
            { task.CipherId.Value, cipherPermissions }
        });

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Create },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Admin), BitAutoData]
    public async Task Create_Admin_MissingCipher_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>());

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Create },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Admin), BitAutoData]
    public async Task Create_Admin_Success(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };
        var cipherPermissions = new OrganizationCipherPermission
        {
            Id = task.CipherId.Value,
            Read = true,
            Edit = true,
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>
        {
            { task.CipherId.Value, cipherPermissions }
        });

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Create },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

        [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.User), BitAutoData]
    public async Task Update_User_CanEditCipher_Success(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };
        var cipherPermissions = new OrganizationCipherPermission
        {
            Id = task.CipherId.Value,
            Read = true,
            Edit = true
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>
        {
            { task.CipherId.Value, cipherPermissions }
        });

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Update },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Admin), BitAutoData]
    public async Task Update_Admin_CanEditCipher_Success(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };
        var cipherPermissions = new OrganizationCipherPermission
        {
            Id = task.CipherId.Value,
            Edit = true
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>
        {
            { task.CipherId.Value, cipherPermissions }
        });

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Update },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.Admin), BitAutoData]
    public async Task Read_Admin_ReadonlyCipher_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>());

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Update },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Theory, CurrentContextOrganizationCustomize(Type = OrganizationUserType.User), BitAutoData]
    public async Task Update_User_CannotEditCipher_Failure(
        CurrentContextOrganization organization,
        SutProvider<SecurityTaskAuthorizationHandler> sutProvider)
    {
        var userId = Guid.NewGuid();
        var task = new SecurityTask
        {
            OrganizationId = organization.Id,
            CipherId = Guid.NewGuid()
        };
        var cipherPermissions = new OrganizationCipherPermission
        {
            Id = task.CipherId.Value,
            Read = true,
            Edit = false
        };

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organization.Id).Returns(new Dictionary<Guid, OrganizationCipherPermission>
        {
            { task.CipherId.Value, cipherPermissions }
        });

        var context = new AuthorizationHandlerContext(
            new[] { SecurityTaskOperations.Update },
            new ClaimsPrincipal(),
            task);

        await sutProvider.Sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
