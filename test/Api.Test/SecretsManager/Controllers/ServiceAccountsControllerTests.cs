using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(ServiceAccountsController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
public class ServiceAccountsControllerTests
{
    [Theory]
    [BitAutoData]
    public async void GetServiceAccountsByOrganization_ReturnsEmptyList(SutProvider<ServiceAccountsController> sutProvider, Guid id)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        var result = await sutProvider.Sut.GetServiceAccountsByOrganizationAsync(id);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
                     .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)), Arg.Any<Guid>(), Arg.Any<AccessClientType>());

        Assert.Empty(result.Data);
    }

    [Theory]
    [BitAutoData]
    public async void GetServiceAccountsByOrganization_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccount resultServiceAccount)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<IServiceAccountRepository>().GetManyByOrganizationIdAsync(default, default, default).ReturnsForAnyArgs(new List<ServiceAccount>() { resultServiceAccount });

        var result = await sutProvider.Sut.GetServiceAccountsByOrganizationAsync(resultServiceAccount.OrganizationId);

        await sutProvider.GetDependency<IServiceAccountRepository>().Received(1)
                     .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultServiceAccount.OrganizationId)), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
        Assert.NotEmpty(result.Data);
        Assert.Single(result.Data);
    }


    [Theory]
    [BitAutoData]
    public async void CreateServiceAccount_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccountCreateRequestModel data, Guid organizationId)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
        var resultServiceAccount = data.ToServiceAccount(organizationId);
        sutProvider.GetDependency<ICreateServiceAccountCommand>().CreateAsync(default, default).ReturnsForAnyArgs(resultServiceAccount);

        var result = await sutProvider.Sut.CreateServiceAccountAsync(organizationId, data);
        await sutProvider.GetDependency<ICreateServiceAccountCommand>().Received(1)
                     .CreateAsync(Arg.Any<ServiceAccount>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateServiceAccount_NotOrgUser_Throws(SutProvider<ServiceAccountsController> sutProvider, ServiceAccountCreateRequestModel data, Guid organizationId)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(false);
        var resultServiceAccount = data.ToServiceAccount(organizationId);
        sutProvider.GetDependency<ICreateServiceAccountCommand>().CreateAsync(default, default).ReturnsForAnyArgs(resultServiceAccount);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateServiceAccountAsync(organizationId, data));

        await sutProvider.GetDependency<ICreateServiceAccountCommand>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<ServiceAccount>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateServiceAccount_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccountUpdateRequestModel data, Guid serviceAccountId)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        var resultServiceAccount = data.ToServiceAccount(serviceAccountId);
        sutProvider.GetDependency<IUpdateServiceAccountCommand>().UpdateAsync(default, default).ReturnsForAnyArgs(resultServiceAccount);

        var result = await sutProvider.Sut.UpdateServiceAccountAsync(serviceAccountId, data);
        await sutProvider.GetDependency<IUpdateServiceAccountCommand>().Received(1)
                     .UpdateAsync(Arg.Any<ServiceAccount>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void CreateAccessToken_Success(SutProvider<ServiceAccountsController> sutProvider, AccessTokenCreateRequestModel data, Guid serviceAccountId)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        var resultAccessToken = data.ToApiKey(serviceAccountId);

        sutProvider.GetDependency<ICreateAccessTokenCommand>().CreateAsync(default, default).ReturnsForAnyArgs(resultAccessToken);

        var result = await sutProvider.Sut.CreateAccessTokenAsync(serviceAccountId, data);
        await sutProvider.GetDependency<ICreateAccessTokenCommand>().Received(1)
            .CreateAsync(Arg.Any<ApiKey>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessTokens_NoServiceAccount_ThrowsNotFound(SutProvider<ServiceAccountsController> sutProvider, Guid serviceAccountId)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(Guid.NewGuid());
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAccessTokens(serviceAccountId));

        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().GetManyByServiceAccountIdAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessTokens_Admin_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccount data, Guid userId, ICollection<ApiKey> resultApiKeys)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(true);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        foreach (var apiKey in resultApiKeys)
        {
            apiKey.Scope = "[\"api.secrets\"]";
        }
        sutProvider.GetDependency<IApiKeyRepository>().GetManyByServiceAccountIdAsync(default).ReturnsForAnyArgs(resultApiKeys);

        var result = await sutProvider.Sut.GetAccessTokens(data.Id);
        await sutProvider.GetDependency<IApiKeyRepository>().Received(1).GetManyByServiceAccountIdAsync(Arg.Any<Guid>());
        Assert.NotEmpty(result.Data);
        Assert.Equal(resultApiKeys.Count, result.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessTokens_User_Success(SutProvider<ServiceAccountsController> sutProvider, ServiceAccount data, Guid userId, ICollection<ApiKey> resultApiKeys)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<IServiceAccountRepository>().UserHasReadAccessToServiceAccount(default, default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        foreach (var apiKey in resultApiKeys)
        {
            apiKey.Scope = "[\"api.secrets\"]";
        }
        sutProvider.GetDependency<IApiKeyRepository>().GetManyByServiceAccountIdAsync(default).ReturnsForAnyArgs(resultApiKeys);

        var result = await sutProvider.Sut.GetAccessTokens(data.Id);
        await sutProvider.GetDependency<IApiKeyRepository>().Received(1).GetManyByServiceAccountIdAsync(Arg.Any<Guid>());
        Assert.NotEmpty(result.Data);
        Assert.Equal(resultApiKeys.Count, result.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async void GetAccessTokens_UserNoAccess_ThrowsNotFound(SutProvider<ServiceAccountsController> sutProvider, ServiceAccount data, Guid userId, ICollection<ApiKey> resultApiKeys)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(data.OrganizationId).Returns(false);
        sutProvider.GetDependency<IServiceAccountRepository>().UserHasReadAccessToServiceAccount(default, default).ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        foreach (var apiKey in resultApiKeys)
        {
            apiKey.Scope = "[\"api.secrets\"]";
        }
        sutProvider.GetDependency<IApiKeyRepository>().GetManyByServiceAccountIdAsync(default).ReturnsForAnyArgs(resultApiKeys);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAccessTokens(data.Id));

        await sutProvider.GetDependency<IApiKeyRepository>().DidNotReceiveWithAnyArgs().GetManyByServiceAccountIdAsync(Arg.Any<Guid>());
    }
}
