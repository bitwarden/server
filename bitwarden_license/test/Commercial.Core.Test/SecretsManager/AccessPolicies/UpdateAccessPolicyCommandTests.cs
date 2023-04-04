using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Commercial.Core.Test.SecretsManager.Enums;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class UpdateAccessPolicyCommandTests
{
    private static BaseAccessPolicy CreatePolicyToReturn(AccessPolicyType accessPolicyType,
        ServiceAccount grantedServiceAccount, Guid data, Group mockGroup)
    {
        switch (accessPolicyType)
        {
            case AccessPolicyType.UserServiceAccountAccessPolicy:
                return
                    new UserServiceAccountAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedServiceAccountId = grantedServiceAccount.Id,
                        GrantedServiceAccount = grantedServiceAccount,
                    };
            case AccessPolicyType.GroupServiceAccountAccessPolicy:
                mockGroup.OrganizationId = grantedServiceAccount.OrganizationId;
                return new GroupServiceAccountAccessPolicy
                {
                    Id = data,
                    GrantedServiceAccountId = grantedServiceAccount.Id,
                    GrantedServiceAccount = grantedServiceAccount,
                    Read = true,
                    Write = true,
                    Group = mockGroup,
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(accessPolicyType), accessPolicyType, null);
        }
    }

    private static BaseAccessPolicy CreatePolicyToReturn(AccessPolicyType accessPolicyType, Guid data,
        Project grantedProject, Group mockGroup, ServiceAccount mockServiceAccount)
    {
        switch (accessPolicyType)
        {
            case AccessPolicyType.UserProjectAccessPolicy:
                return
                    new UserProjectAccessPolicy
                    {
                        Id = data,
                        Read = true,
                        Write = true,
                        GrantedProjectId = grantedProject.Id,
                        GrantedProject = grantedProject,
                    };
            case AccessPolicyType.GroupProjectAccessPolicy:
                mockGroup.OrganizationId = grantedProject.OrganizationId;
                return
                    new GroupProjectAccessPolicy
                    {
                        Id = data,
                        GrantedProjectId = grantedProject.Id,
                        Read = true,
                        Write = true,
                        Group = mockGroup,
                        GrantedProject = grantedProject,
                    };
            case AccessPolicyType.ServiceAccountProjectAccessPolicy:
                mockServiceAccount.OrganizationId = grantedProject.OrganizationId;
                return new ServiceAccountProjectAccessPolicy
                {
                    Id = data,
                    GrantedProjectId = grantedProject.Id,
                    Read = true,
                    Write = true,
                    ServiceAccount = mockServiceAccount,
                    GrantedProject = grantedProject,
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(accessPolicyType), accessPolicyType, null);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Throws_NotFoundException(Guid data, bool read, bool write,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data, read, write));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupProjectAccessPolicy)]
    [BitAutoData(AccessPolicyType.ServiceAccountProjectAccessPolicy)]
    public async Task UpdateAsync_ProjectGrants_Success(
        AccessPolicyType accessPolicyType,
        Guid data,
        bool read,
        bool write,
        Project grantedProject,
        Group mockGroup,
        ServiceAccount mockServiceAccount,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        var policyToReturn =
            CreatePolicyToReturn(accessPolicyType, data, grantedProject, mockGroup, mockServiceAccount);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(policyToReturn);
        var result = await sutProvider.Sut.UpdateAsync(data, read, write);
        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).ReplaceAsync(policyToReturn);

        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.Equal(read, result.Read);
        Assert.Equal(write, result.Write);
    }

    [Theory]
    [BitAutoData(AccessPolicyType.UserServiceAccountAccessPolicy)]
    [BitAutoData(AccessPolicyType.GroupServiceAccountAccessPolicy)]
    public async Task UpdateAsync_ServiceAccountGrants_PermissionsCheck_Success(
        AccessPolicyType accessPolicyType,
        Guid data,
        bool read,
        bool write,
        ServiceAccount grantedServiceAccount,
        Group mockGroup,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        var policyToReturn = CreatePolicyToReturn(accessPolicyType, grantedServiceAccount, data, mockGroup);

        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(policyToReturn);
        var result = await sutProvider.Sut.UpdateAsync(data, read, write);
        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).ReplaceAsync(policyToReturn);

        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.Equal(read, result.Read);
        Assert.Equal(write, result.Write);
    }
}
