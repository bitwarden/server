using Bit.Commercial.Core.SecretManager.Commands.AccessPolicies;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManager.AccessPolicies;

[SutProviderCustomize]
public class CreateAccessPoliciesCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_CallsCreate(List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        await sutProvider.Sut.CreateAsync(data);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_AlreadyExists_Throws_BadRequestException(
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(data));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
    }


    [Theory]
    [BitAutoData(true, false, false)]
    [BitAutoData(false, true, false)]
    [BitAutoData(true, true, false)]
    [BitAutoData(false, false, true)]
    [BitAutoData(true, false, true)]
    [BitAutoData(false, true, true)]
    [BitAutoData(true, true, true)]
    public async Task CreateAsync_NotUnique_ThrowsException(
        bool testUserPolicies,
        bool testGroupPolicies,
        bool testServiceAccountPolicies,
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider
    )
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        if (testUserPolicies)
        {
            var mockUserPolicy = new UserProjectAccessPolicy
            {
                OrganizationUserId = Guid.NewGuid(),
                GrantedProjectId = Guid.NewGuid()
            };
            data.Add(mockUserPolicy);

            // Add a duplicate policy
            data.Add(mockUserPolicy);
        }

        if (testGroupPolicies)
        {
            var mockGroupPolicy = new GroupProjectAccessPolicy
            {
                GroupId = Guid.NewGuid(),
                GrantedProjectId = Guid.NewGuid()
            };
            data.Add(mockGroupPolicy);

            // Add a duplicate policy
            data.Add(mockGroupPolicy);
        }

        if (testServiceAccountPolicies)
        {
            var mockServiceAccountPolicy = new ServiceAccountProjectAccessPolicy
            {
                ServiceAccountId = Guid.NewGuid(),
                GrantedProjectId = Guid.NewGuid()
            };
            data.Add(mockServiceAccountPolicy);

            // Add a duplicate policy
            data.Add(mockServiceAccountPolicy);
        }


        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(data));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
    }
}
