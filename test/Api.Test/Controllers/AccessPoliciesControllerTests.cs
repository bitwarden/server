using Bit.Api.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(AccessPoliciesController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
public class AccessPoliciesControllerTests
{
    [Theory]
    [BitAutoData]
    public async void GetAccessPoliciesByProject_ReturnsEmptyList(SutProvider<AccessPoliciesController> sutProvider,
        Guid id)
    {
        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByProjectId(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.GroupAccessPolicies);
        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessPoliciesByProject_Success(SutProvider<AccessPoliciesController> sutProvider, Guid id,
        UserProjectAccessPolicy resultAccessPolicy)
    {
        sutProvider.GetDependency<IAccessPolicyRepository>().GetManyByProjectId(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { resultAccessPolicy });

        var result = await sutProvider.Sut.GetProjectAccessPoliciesAsync(id);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .GetManyByProjectId(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.GroupAccessPolicies);
        Assert.NotEmpty(result.UserAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [BitAutoData]
    public async void CreateAccessPolicies_Success(SutProvider<AccessPoliciesController> sutProvider, Guid id,
        UserProjectAccessPolicy data, AccessPoliciesCreateRequest request)
    {
        sutProvider.GetDependency<ICreateAccessPoliciesCommand>().CreateAsync(default)
            .ReturnsForAnyArgs(new List<BaseAccessPolicy> { data });
        var result = await sutProvider.Sut.CreateProjectAccessPoliciesAsync(id, request);
        await sutProvider.GetDependency<ICreateAccessPoliciesCommand>().Received(1)
            .CreateAsync(Arg.Any<List<BaseAccessPolicy>>());
    }


    [Theory]
    [BitAutoData]
    public async void UpdateAccessPolicies_Success(SutProvider<AccessPoliciesController> sutProvider, Guid id,
        UserProjectAccessPolicy data, AccessPolicyUpdateRequest request)
    {
        sutProvider.GetDependency<IUpdateAccessPolicyCommand>().UpdateAsync(default, default, default)
            .ReturnsForAnyArgs(data);
        var result = await sutProvider.Sut.UpdateAccessPolicyAsync(id, request);
        await sutProvider.GetDependency<IUpdateAccessPolicyCommand>().Received(1)
            .UpdateAsync(Arg.Any<Guid>(), Arg.Is(request.Read), Arg.Is(request.Write));
    }

    [Theory]
    [BitAutoData]
    public async void DeleteAccessPolicies_Success(SutProvider<AccessPoliciesController> sutProvider, Guid id)
    {
        sutProvider.GetDependency<IDeleteAccessPolicyCommand>().DeleteAsync(default).ReturnsNull();
        await sutProvider.Sut.DeleteAccessPolicyAsync(id);
        await sutProvider.GetDependency<IDeleteAccessPolicyCommand>().Received(1)
            .DeleteAsync(Arg.Any<Guid>());
    }
}
