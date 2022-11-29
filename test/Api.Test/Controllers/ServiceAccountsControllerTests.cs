using Bit.Api.Controllers;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.AccessTokens.Interfaces;
using Bit.Core.SecretManagerFeatures.ServiceAccounts.Interfaces;
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


    [Theory]
    [BitAutoData]
    public async void CreateServiceAccount_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccountCreateRequestModel data, Guid organizationId)
    {
        var resultServiceAccount = data.ToServiceAccount(organizationId);

        sutProvider.GetDependency<ICreateServiceAccountCommand>().CreateAsync(default).ReturnsForAnyArgs(resultServiceAccount);

        var result = await sutProvider.Sut.CreateServiceAccountAsync(organizationId, data);
        await sutProvider.GetDependency<ICreateServiceAccountCommand>().Received(1)
                     .CreateAsync(Arg.Any<ServiceAccount>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateServiceAccount_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccountUpdateRequestModel data, Guid secretId)
    {
        var resultServiceAccount = data.ToServiceAccount(secretId);
        sutProvider.GetDependency<IUpdateServiceAccountCommand>().UpdateAsync(default).ReturnsForAnyArgs(resultServiceAccount);

        var result = await sutProvider.Sut.UpdateServiceAccountAsync(secretId, data);
        await sutProvider.GetDependency<IUpdateServiceAccountCommand>().Received(1)
                     .UpdateAsync(Arg.Any<ServiceAccount>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateAccessToken_Success(SutProvider<ServiceAccountsController> sutProvider, AccessTokenCreateRequestModel data, Guid serviceAccountId)
    {
        var resultAccessToken = data.ToApiKey(serviceAccountId);

        sutProvider.GetDependency<ICreateAccessTokenCommand>().CreateAsync(default).ReturnsForAnyArgs(resultAccessToken);

        var result = await sutProvider.Sut.CreateAccessTokenAsync(serviceAccountId, data);
        await sutProvider.GetDependency<ICreateAccessTokenCommand>().Received(1)
            .CreateAsync(Arg.Any<ApiKey>());
    }
}
