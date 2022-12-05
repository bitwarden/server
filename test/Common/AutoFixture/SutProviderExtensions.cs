using AutoFixture;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using RichardSzalay.MockHttp;

namespace Bit.Test.Common.AutoFixture;

public static class SutProviderExtensions
{
    public static SutProvider<T> ConfigureBaseIdentityClientService<T>(this SutProvider<T> sutProvider,
        string requestUrlFragment, HttpMethod requestHttpMethod, string identityResponse = null, string apiResponse = null)
        where T : BaseIdentityClientService
    {
        var fixture = new Fixture().WithAutoNSubstitutionsAutoPopulatedProperties();
        fixture.AddMockHttp();

        var settings = fixture.Create<IGlobalSettings>();
        settings.SelfHosted = true;
        settings.EnableCloudCommunication = true;

        var apiUri = fixture.Create<Uri>();
        var identityUri = fixture.Create<Uri>();
        settings.Installation.ApiUri.Returns(apiUri.ToString());
        settings.Installation.IdentityUri.Returns(identityUri.ToString());

        var apiHandler = new MockHttpMessageHandler();
        var identityHandler = new MockHttpMessageHandler();
        var syncUri = string.Concat(apiUri, requestUrlFragment);
        var tokenUri = string.Concat(identityUri, "connect/token");

        apiHandler.When(requestHttpMethod, syncUri)
            .Respond("application/json", apiResponse);
        identityHandler.When(HttpMethod.Post, tokenUri)
            .Respond("application/json", identityResponse ?? "{\"access_token\":\"string\",\"expires_in\":3600,\"token_type\":\"Bearer\",\"scope\":\"string\"}");


        var apiHttp = apiHandler.ToHttpClient();
        var identityHttp = identityHandler.ToHttpClient();

        var mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        mockHttpClientFactory.CreateClient(Arg.Is("client")).Returns(apiHttp);
        mockHttpClientFactory.CreateClient(Arg.Is("identity")).Returns(identityHttp);

        return sutProvider
            .SetDependency(settings)
            .SetDependency(mockHttpClientFactory)
            .Create();
    }
}
