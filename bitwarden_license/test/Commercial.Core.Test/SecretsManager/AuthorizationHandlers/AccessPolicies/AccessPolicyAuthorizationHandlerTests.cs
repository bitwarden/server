using System.Reflection;
using System.Security.Claims;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.AuthorizationHandlers.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class AccessPolicyAuthorizationHandlerTests
{
    private static void SetupCurrentUserPermission(SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        PermissionType permissionType, Guid organizationId, Guid userId = new())
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(ClientType.User);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
                break;
            case PermissionType.RunAsUserWithPermission:
                sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }
    }

    private static BaseAccessPolicy CreatePolicy(AccessPolicyType accessPolicyType, Project grantedProject,
        ServiceAccount grantedServiceAccount, Guid? serviceAccountId = null)
    {
        switch (accessPolicyType)
        {
            case AccessPolicyType.UserProjectAccessPolicy:
                return
                    new UserProjectAccessPolicy
                    {
                        Id = Guid.NewGuid(),
                        OrganizationUserId = Guid.NewGuid(),
                        Read = true,
                        Write = true,
                        GrantedProjectId = grantedProject.Id,
                        GrantedProject = grantedProject,
                    };
            case AccessPolicyType.GroupProjectAccessPolicy:
                return
                    new GroupProjectAccessPolicy
                    {
                        Id = Guid.NewGuid(),
                        GroupId = Guid.NewGuid(),
                        GrantedProjectId = grantedProject.Id,
                        Read = true,
                        Write = true,
                        GrantedProject = grantedProject,
                    };
            case AccessPolicyType.ServiceAccountProjectAccessPolicy:
                return new ServiceAccountProjectAccessPolicy
                {
                    Id = Guid.NewGuid(),
                    ServiceAccountId = serviceAccountId,
                    GrantedProjectId = grantedProject.Id,
                    Read = true,
                    Write = true,
                    GrantedProject = grantedProject,
                };
            case AccessPolicyType.UserServiceAccountAccessPolicy:
                return
                    new UserServiceAccountAccessPolicy
                    {
                        Id = Guid.NewGuid(),
                        OrganizationUserId = Guid.NewGuid(),
                        Read = true,
                        Write = true,
                        GrantedServiceAccountId = grantedServiceAccount.Id,
                        GrantedServiceAccount = grantedServiceAccount,
                    };
            case AccessPolicyType.GroupServiceAccountAccessPolicy:
                return new GroupServiceAccountAccessPolicy
                {
                    Id = Guid.NewGuid(),
                    GroupId = Guid.NewGuid(),
                    GrantedServiceAccountId = grantedServiceAccount.Id,
                    GrantedServiceAccount = grantedServiceAccount,
                    Read = true,
                    Write = true,
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(accessPolicyType), accessPolicyType, null);
        }
    }

    private static void SetupMockAccess(SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid userId, BaseAccessPolicy accessPolicy, bool read, bool write)
    {
        switch (accessPolicy)
        {
            case UserProjectAccessPolicy ap:
                sutProvider.GetDependency<IProjectRepository>()
                    .AccessToProjectAsync(ap.GrantedProjectId!.Value, userId, Arg.Any<AccessClientType>())
                    .Returns((read, write));
                break;
            case GroupProjectAccessPolicy ap:
                sutProvider.GetDependency<IProjectRepository>()
                    .AccessToProjectAsync(ap.GrantedProjectId!.Value, userId, Arg.Any<AccessClientType>())
                    .Returns((read, write));
                break;
            case UserServiceAccountAccessPolicy ap:
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .AccessToServiceAccountAsync(ap.GrantedServiceAccountId!.Value, userId, Arg.Any<AccessClientType>())
                    .Returns((read, write));
                break;
            case GroupServiceAccountAccessPolicy ap:
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .AccessToServiceAccountAsync(ap.GrantedServiceAccountId!.Value, userId, Arg.Any<AccessClientType>())
                    .Returns((read, write));
                break;
            case ServiceAccountProjectAccessPolicy ap:
                sutProvider.GetDependency<IProjectRepository>()
                    .AccessToProjectAsync(ap.GrantedProjectId!.Value, userId, Arg.Any<AccessClientType>())
                    .Returns((read, write));
                sutProvider.GetDependency<IServiceAccountRepository>()
                    .AccessToServiceAccountAsync(ap.ServiceAccountId!.Value, userId, Arg.Any<AccessClientType>())
                    .Returns((read, write));
                break;
        }
    }

    private static void SetupOrganizationMismatch(SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        BaseAccessPolicy accessPolicy)
    {
        switch (accessPolicy)
        {
            case UserProjectAccessPolicy resource:
                sutProvider.GetDependency<IOrganizationUserRepository>()
                    .GetByIdAsync(resource.OrganizationUserId!.Value)
                    .Returns(new OrganizationUser
                    {
                        Id = resource.OrganizationUserId!.Value,
                        OrganizationId = Guid.NewGuid()
                    });
                break;
            case GroupProjectAccessPolicy resource:
                sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(resource.GroupId!.Value)
                    .Returns(new Group { Id = resource.GroupId!.Value, OrganizationId = Guid.NewGuid() });
                break;
            case UserServiceAccountAccessPolicy resource:
                sutProvider.GetDependency<IOrganizationUserRepository>()
                    .GetByIdAsync(resource.OrganizationUserId!.Value)
                    .Returns(new OrganizationUser
                    {
                        Id = resource.OrganizationUserId!.Value,
                        OrganizationId = Guid.NewGuid()
                    });
                break;
            case GroupServiceAccountAccessPolicy resource:
                sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(resource.GroupId!.Value)
                    .Returns(new Group { Id = resource.GroupId!.Value, OrganizationId = Guid.NewGuid() });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(accessPolicy), accessPolicy, null);
        }
    }

    private static void SetupOrganizationMatch(SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        BaseAccessPolicy accessPolicy, Guid organizationId)
    {
        switch (accessPolicy)
        {
            case UserProjectAccessPolicy resource:
                sutProvider.GetDependency<IOrganizationUserRepository>()
                    .GetByIdAsync(resource.OrganizationUserId!.Value)
                    .Returns(new OrganizationUser
                    {
                        Id = resource.OrganizationUserId!.Value,
                        OrganizationId = organizationId
                    });
                break;
            case GroupProjectAccessPolicy resource:
                sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(resource.GroupId!.Value)
                    .Returns(new Group { Id = resource.GroupId!.Value, OrganizationId = organizationId });
                break;
            case UserServiceAccountAccessPolicy resource:
                sutProvider.GetDependency<IOrganizationUserRepository>()
                    .GetByIdAsync(resource.OrganizationUserId!.Value)
                    .Returns(new OrganizationUser
                    {
                        Id = resource.OrganizationUserId!.Value,
                        OrganizationId = organizationId
                    });
                break;
            case GroupServiceAccountAccessPolicy resource:
                sutProvider.GetDependency<IGroupRepository>().GetByIdAsync(resource.GroupId!.Value)
                    .Returns(new Group { Id = resource.GroupId!.Value, OrganizationId = organizationId });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(accessPolicy), accessPolicy, null);
        }
    }

    [Fact]
    public void AccessPolicyOperations_OnlyPublicStatic()
    {
        var publicStaticFields = typeof(AccessPolicyOperations).GetFields(BindingFlags.Public | BindingFlags.Static);
        var allFields = typeof(AccessPolicyOperations).GetFields();
        Assert.Equal(publicStaticFields.Length, allFields.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task Handler_UnsupportedAccessPolicyOperationRequirement_Throws(
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider, UserProjectAccessPolicy resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = new AccessPolicyOperationRequirement();
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await Assert.ThrowsAsync<ArgumentException>(() => sutProvider.Sut.HandleAsync(authzContext));
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CanCreate_OrgMismatch_DoesNotSucceed(
        AccessPolicyType accessPolicyType,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Create;
        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount);
        SetupOrganizationMismatch(sutProvider, resource);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CanCreate_AccessToSecretsManagerFalse_DoesNotSucceed(
        AccessPolicyType accessPolicyType,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Create;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;
        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount);
        SetupOrganizationMatch(sutProvider, resource, organizationId);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(false);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.GroupServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CanCreate_UnsupportedClientTypes_DoesNotSucceed(
        ClientType clientType,
        AccessPolicyType accessPolicyType,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Create;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;
        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount);
        SetupOrganizationMatch(sutProvider, resource, organizationId);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(new Guid());

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    public async Task CanCreate_AccessCheck(
        AccessPolicyType accessPolicyType,
        PermissionType permissionType,
        bool read, bool write, bool expected,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Guid userId,
        Guid serviceAccountId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Create;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;
        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount, serviceAccountId);
        SetupCurrentUserPermission(sutProvider, permissionType, organizationId, userId);
        SetupOrganizationMatch(sutProvider, resource, organizationId);
        SetupMockAccess(sutProvider, userId, resource, read, write);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false, false)]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    public async Task CanCreate_ServiceAccountProjectAccessPolicy_TargetsDontExist_DoesNotSucceed(bool projectExists,
        bool serviceAccountExists,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider, ServiceAccountProjectAccessPolicy resource,
        Project mockProject, ServiceAccount mockServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Create;
        resource.GrantedProject = null;
        resource.ServiceAccount = null;

        if (projectExists)
        {
            resource.GrantedProject = null;
            mockProject.Id = resource.GrantedProjectId!.Value;
            sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(resource.GrantedProjectId!.Value)
                .Returns(mockProject);
        }

        if (serviceAccountExists)
        {
            resource.ServiceAccount = null;
            sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(resource.ServiceAccountId!.Value)
                .Returns(mockServiceAccount);
        }

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(false, false)]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    [BitAutoData(true, true)]
    public async Task CanCreate_ServiceAccountProjectAccessPolicy_OrgMismatch_DoesNotSucceed(bool fetchProject,
        bool fetchSa,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider, ServiceAccountProjectAccessPolicy resource,
        Project mockProject, ServiceAccount mockServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Create;

        if (fetchProject)
        {
            resource.GrantedProject = null;
            mockProject.Id = resource.GrantedProjectId!.Value;
            sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(resource.GrantedProjectId!.Value)
                .Returns(mockProject);
        }

        if (fetchSa)
        {
            resource.ServiceAccount = null;
            mockServiceAccount.Id = resource.ServiceAccountId!.Value;
            sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(resource.ServiceAccountId!.Value)
                .Returns(mockServiceAccount);
        }

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task CanCreate_ServiceAccountProjectAccessPolicy_AccessToSecretsManagerFalse_DoesNotSucceed(
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider, ServiceAccountProjectAccessPolicy resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Create;
        resource.ServiceAccount!.OrganizationId = resource.GrantedProject!.OrganizationId;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.GrantedProject!.OrganizationId)
            .Returns(false);
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount)]
    [BitAutoData(ClientType.Organization)]
    public async Task CanCreate_ServiceAccountProjectAccessPolicy_UnsupportedClientTypes_DoesNotSucceed(
        ClientType clientType,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider, ServiceAccountProjectAccessPolicy resource,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Create;
        resource.ServiceAccount!.OrganizationId = resource.GrantedProject!.OrganizationId;
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(resource.GrantedProject!.OrganizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(new Guid());
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(PermissionType.RunAsAdmin, true, true, true, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, false, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, false, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, false, true, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, false, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, false, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, false, true, true, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, false, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, false, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, false, true, true, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, false, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, false, true, true)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, true, false, false)]
    [BitAutoData(PermissionType.RunAsUserWithPermission, true, true, true, true, true)]
    public async Task CanCreate_ServiceAccountProjectAccessPolicy_AccessCheck(PermissionType permissionType,
        bool projectRead,
        bool projectWrite, bool saRead, bool saWrite, bool expected,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider, ServiceAccountProjectAccessPolicy resource,
        ClaimsPrincipal claimsPrincipal, Guid userId)
    {
        var requirement = AccessPolicyOperations.Create;
        resource.ServiceAccount!.OrganizationId = resource.GrantedProject!.OrganizationId;
        SetupCurrentUserPermission(sutProvider, permissionType, resource.GrantedProject!.OrganizationId, userId);
        sutProvider.GetDependency<IProjectRepository>()
            .AccessToProjectAsync(resource.GrantedProjectId!.Value, userId, Arg.Any<AccessClientType>())
            .Returns((projectRead, projectWrite));
        sutProvider.GetDependency<IServiceAccountRepository>()
            .AccessToServiceAccountAsync(resource.ServiceAccountId!.Value, userId, Arg.Any<AccessClientType>())
            .Returns((saRead, saWrite));
        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CanUpdate_AccessToSecretsManagerFalse_DoesNotSucceed(AccessPolicyType accessPolicyType,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Update;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;
        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(false);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.GroupServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CanUpdate_UnsupportedClientTypes_DoesNotSucceed(
        ClientType clientType,
        AccessPolicyType accessPolicyType,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Update;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;
        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(new Guid());

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    public async Task CanUpdate_AccessCheck(
        AccessPolicyType accessPolicyType,
        PermissionType permissionType, bool read,
        bool write, bool expected,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal, Guid userId, Guid serviceAccountId)
    {
        var requirement = AccessPolicyOperations.Update;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;

        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount,
            serviceAccountId);
        SetupCurrentUserPermission(sutProvider, permissionType, organizationId, userId);
        SetupMockAccess(sutProvider, userId, resource, read, write);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CanDelete_AccessToSecretsManagerFalse_DoesNotSucceed(AccessPolicyType accessPolicyType,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Delete;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;
        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(false);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.ServiceAccount, AccessPolicyType.GroupServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(ClientType.Organization, AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task CanDelete_UnsupportedClientTypes_DoesNotSucceed(
        ClientType clientType,
        AccessPolicyType accessPolicyType,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal)
    {
        var requirement = AccessPolicyOperations.Delete;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;
        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount);
        sutProvider.GetDependency<ICurrentContext>().AccessSecretsManager(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ClientType
            .Returns(clientType);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(new Guid());

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.False(authzContext.HasSucceeded);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsAdmin, true, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, false, false)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, false, true, true)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, false, false)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy, PermissionType.RunAsUserWithPermission, true, true, true)]
    public async Task CanDelete_AccessCheck(
        AccessPolicyType accessPolicyType,
        PermissionType permissionType,
        bool read, bool write, bool expected,
        SutProvider<AccessPolicyAuthorizationHandler> sutProvider,
        Guid organizationId,
        Project mockGrantedProject,
        ServiceAccount mockGrantedServiceAccount,
        ClaimsPrincipal claimsPrincipal, Guid userId, Guid serviceAccountId)
    {
        var requirement = AccessPolicyOperations.Delete;
        mockGrantedProject.OrganizationId = organizationId;
        mockGrantedServiceAccount.OrganizationId = organizationId;

        var resource = CreatePolicy(accessPolicyType, mockGrantedProject, mockGrantedServiceAccount,
            serviceAccountId);
        SetupCurrentUserPermission(sutProvider, permissionType, organizationId, userId);
        SetupMockAccess(sutProvider, userId, resource, read, write);

        var authzContext = new AuthorizationHandlerContext(new List<IAuthorizationRequirement> { requirement },
            claimsPrincipal, resource);

        await sutProvider.Sut.HandleAsync(authzContext);

        Assert.Equal(expected, authzContext.HasSucceeded);
    }
}
