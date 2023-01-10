using Bit.Commercial.Core.SecretManagerFeatures.AccessPolicies;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManagerFeatures.AccessPolicies;

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
    [BitAutoData]
    public async Task CreateAsync_NotUnique_ThrowsException(
        List<UserProjectAccessPolicy> userProjectAccessPolicies,
        List<GroupProjectAccessPolicy> groupProjectAccessPolicies,
        List<ServiceAccountProjectAccessPolicy> serviceAccountProjectAccessPolicies,
        SutProvider<CreateAccessPoliciesCommand> sutProvider)
    {
        var data = new List<BaseAccessPolicy>();
        data.AddRange(userProjectAccessPolicies);
        data.AddRange(groupProjectAccessPolicies);
        data.AddRange(serviceAccountProjectAccessPolicies);

        var mockUserPolicy = new UserProjectAccessPolicy()
        {
            OrganizationUserId = Guid.NewGuid(),
            GrantedProjectId = Guid.NewGuid()
        };
        data.Add(mockUserPolicy);

        // Add a duplicate policy
        data.Add(mockUserPolicy);

        sutProvider.GetDependency<IAccessPolicyRepository>().AccessPolicyExists(Arg.Any<BaseAccessPolicy>())
            .Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(data));

        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().CreateManyAsync(default);
    }
}
