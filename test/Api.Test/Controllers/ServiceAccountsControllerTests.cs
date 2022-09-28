using Bit.Api.Controllers;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(ServiceAccountsController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
public class ServiceAccountsControllerTests
{
    [Theory]
    [BitAutoData]
    public async void GetServiceAccountsByOrganization_ReturnsEmptyList(SutProvider<ServiceAccountsController> sutProvider, Guid id)
    {
        var result = await sutProvider.Sut.GetServiceAccountsByOrganizationAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
                     .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetServiceAccountsByOrganization_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccount resultServiceAccount)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetManyByOrganizationIdAsync(default).ReturnsForAnyArgs(new List<ServiceAccount>() { resultServiceAccount });

        var result = await sutProvider.Sut.GetServiceAccountsByOrganizationAsync(resultServiceAccount.OrganizationId);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
                     .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultServiceAccount.OrganizationId)));
    }
}
