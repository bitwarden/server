using Bit.Billing.Controllers;
using Bit.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Billing.Test.Controllers;

[ControllerCustomize(typeof(FreshdeskController))]
[SutProviderCustomize]
public class FreshdeskControllerTests
{
    private const string ApiKey = "TESTFRESHDESKAPIKEY";
    private const string WebhookKey = "TESTKEY";

    [Theory]
    [BitAutoData((string)null, null)]
    [BitAutoData((string)null)]
    [BitAutoData(WebhookKey, null)]
    public async Task PostWebhook_NullRequiredParameters_BadRequest(string freshdeskWebhookKey, FreshdeskWebhookModel model,
        BillingSettings billingSettings, SutProvider<FreshdeskController> sutProvider)
    {
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshdeskWebhookKey.Returns(billingSettings.FreshdeskWebhookKey);

        var response = await sutProvider.Sut.PostWebhook(freshdeskWebhookKey, model);

        var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCodeResult.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task PostWebhook_Success(User user, FreshdeskWebhookModel model,
        List<Organization> organizations, SutProvider<FreshdeskController> sutProvider)
    {
        model.TicketContactEmail = user.Email;

        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(user.Email).Returns(user);
        sutProvider.GetDependency<IOrganizationRepository>().GetManyByUserIdAsync(user.Id).Returns(organizations);

        var mockHttpMessageHandler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        mockHttpMessageHandler.Send(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
           .Returns(mockResponse);
        var httpClient = new HttpClient(mockHttpMessageHandler);

        sutProvider.GetDependency<IHttpClientFactory>().CreateClient("FreshdeskApi").Returns(httpClient);

        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshdeskWebhookKey.Returns(WebhookKey);
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshdeskApiKey.Returns(ApiKey);

        var response = await sutProvider.Sut.PostWebhook(WebhookKey, model);

        var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(response);
        Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);

        _ = mockHttpMessageHandler.Received(1).Send(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Put && m.RequestUri.ToString().EndsWith(model.TicketId)), Arg.Any<CancellationToken>());
        _ = mockHttpMessageHandler.Received(1).Send(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri.ToString().EndsWith($"{model.TicketId}/notes")), Arg.Any<CancellationToken>());
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Send(request, cancellationToken);
        }

        public new virtual Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
