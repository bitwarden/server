using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class CreateAccessPoliciesCommandTests
{
    private static List<BaseAccessPolicy> MakeGrantedProjectAccessPolicies(Guid grantedProjectId, List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies)
    {
        var data = new List<BaseAccessPolicy>();
        foreach (var ap in userProjectAccessPolicies)
        {
            ap.GrantedProjectId = grantedProjectId;
            ap.GrantedProject = null;
            ap.User = null;
        }
        foreach (var ap in groupProjectAccessPolicies)
        {
            ap.GrantedProjectId = grantedProjectId;
            ap.GrantedProject = null;
            ap.Group = null;
        }
        foreach (var ap in serviceAccountProjectAccessPolicies)
        {
            ap.GrantedProjectId = grantedProjectId;
            ap.GrantedProject = null;
            ap.ServiceAccount = null;
        }
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);
        return data;
    }

    private static List<BaseAccessPolicy> MakeGrantedServiceAccountAccessPolicies(Guid grantedServiceAccountId, List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies)
    {
        var data = new List<BaseAccessPolicy>();
        foreach (var ap in userServiceAccountAccessPolicies)
        {
            ap.GrantedServiceAccountId = grantedServiceAccountId;
            ap.GrantedServiceAccount = null;
            ap.User = null;
        }
        foreach (var ap in groupServiceAccountAccessPolicies)
        {
            ap.GrantedServiceAccountId = grantedServiceAccountId;
            ap.GrantedServiceAccount = null;
            ap.Group = null;
        }
        data.AddRange(userServiceAccountAccessPolicies);
        data.AddRange(groupServiceAccountAccessPolicies);
        return data;
    }

    [Theory]
    [BitAutoData]
    public async Task CreateMany_AlreadyExists_Throws_BadRequestException(
        Project project,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = MakeGrantedProjectAccessPolicies(project.Id, userProjectAccessPolicies, groupProjectAccessPolicies,
            serviceAccountProjectAccessPolicies);
        var saData = MakeGrantedServiceAccountAccessPolicies(serviceAccount.Id, userServiceAccountAccessPolicies, groupServiceAccountAccessPolicies);
        data = data.Concat(saData).ToList();

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateManyAsync(data));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateMany_ClearsReferences(SutProvider<CreateAccessPoliciesCommand> sutProvider, Guid projectId)
    {
        var userProjectAp = new UserProjectAccessPolicy
        {
            GrantedProjectId = projectId,
            OrganizationUserId = new Guid(),
        };
        var data = new List<BaseAccessPolicy>() { userProjectAp, };

        userProjectAp.GrantedProject = new Project() { Id = new Guid() };
        var expectedCall = new List<BaseAccessPolicy>() { userProjectAp, };

        await sutProvider.Sut.CreateManyAsync(data);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(expectedCall)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateMany_Success(
        Project project,
        ServiceAccount serviceAccount,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        List<UserServiceAccountAccessPolicy> userServiceAccountAccessPolicies,
        List<GroupServiceAccountAccessPolicy> groupServiceAccountAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = MakeGrantedProjectAccessPolicies(project.Id, userProjectAccessPolicies, groupProjectAccessPolicies,
            serviceAccountProjectAccessPolicies);
        var saData = MakeGrantedServiceAccountAccessPolicies(serviceAccount.Id, userServiceAccountAccessPolicies, groupServiceAccountAccessPolicies);
        data = data.Concat(saData).ToList();

        await sutProvider.Sut.CreateManyAsync(data);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }
}
