using Bit.Api.Billing.Controllers;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Models.Api;
using Bit.Test.Common.AutoFixture;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Billing;

public static class Utilities
{
    public static void AssertNotFound(IResult result)
    {
        Assert.IsType<NotFound<ErrorResponseModel>>(result);

        var response = ((NotFound<ErrorResponseModel>)result).Value;

        Assert.Equal("Resource not found.", response.Message);
    }

    public static void AssertUnauthorized(IResult result, string message = "Unauthorized.")
    {
        Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);

        var response = (JsonHttpResult<ErrorResponseModel>)result;

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(message, response.Value.Message);
    }

    public static void ConfigureStableProviderAdminInputs<T>(
        Provider provider,
        SutProvider<T> sutProvider) where T : BaseProviderController
    {
        ConfigureBaseProviderInputs(provider, sutProvider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(true);
    }

    public static void ConfigureStableProviderServiceUserInputs<T>(
        Provider provider,
        SutProvider<T> sutProvider) where T : BaseProviderController
    {
        ConfigureBaseProviderInputs(provider, sutProvider);

        sutProvider.GetDependency<ICurrentContext>().ProviderUser(provider.Id)
            .Returns(true);
    }

    private static void ConfigureBaseProviderInputs<T>(
        Provider provider,
        SutProvider<T> sutProvider) where T : BaseProviderController
    {
        provider.Type = ProviderType.Msp;
        provider.Status = ProviderStatusType.Billable;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
    }
}
