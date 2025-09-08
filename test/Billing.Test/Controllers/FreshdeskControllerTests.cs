using System.Text.Json;
using Bit.Billing.Controllers;
using Bit.Billing.Models;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Xunit;

namespace Bit.Billing.Test.Controllers;

[ControllerCustomize(typeof(FreshdeskController))]
[SutProviderCustomize]
public class FreshdeskControllerTests
{
    private const string ApiKey = "TESTFRESHDESKAPIKEY";
    private const string WebhookKey = "TESTKEY";

    private const string UserFieldName = "cf_user";
    private const string OrgFieldName = "cf_org";

    [Theory]
    [BitAutoData((string)null, null)]
    [BitAutoData((string)null)]
    [BitAutoData(WebhookKey, null)]
    public async Task PostWebhook_NullRequiredParameters_BadRequest(string freshdeskWebhookKey, FreshdeskWebhookModel model,
        BillingSettings billingSettings, SutProvider<FreshdeskController> sutProvider)
    {
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshDesk.WebhookKey.Returns(billingSettings.FreshDesk.WebhookKey);

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

        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshDesk.WebhookKey.Returns(WebhookKey);
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshDesk.ApiKey.Returns(ApiKey);
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshDesk.UserFieldName.Returns(UserFieldName);
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshDesk.OrgFieldName.Returns(OrgFieldName);

        var response = await sutProvider.Sut.PostWebhook(WebhookKey, model);

        var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(response);
        Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);

        _ = mockHttpMessageHandler.Received(1).Send(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Put && m.RequestUri.ToString().EndsWith(model.TicketId)), Arg.Any<CancellationToken>());
        _ = mockHttpMessageHandler.Received(1).Send(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri.ToString().EndsWith($"{model.TicketId}/notes")), Arg.Any<CancellationToken>());
    }

    [Theory]
    [BitAutoData(WebhookKey)]
    public async Task PostWebhook_add_note_when_user_is_invalid(
        string freshdeskWebhookKey, FreshdeskWebhookModel model,
        SutProvider<FreshdeskController> sutProvider)
    {
        // Arrange - for an invalid user
        model.TicketContactEmail = "invalid@user";
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(model.TicketContactEmail).Returns((User)null);
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.FreshDesk.WebhookKey.Returns(WebhookKey);

        var mockHttpMessageHandler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        mockHttpMessageHandler.Send(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
           .Returns(mockResponse);
        var httpClient = new HttpClient(mockHttpMessageHandler);
        sutProvider.GetDependency<IHttpClientFactory>().CreateClient("FreshdeskApi").Returns(httpClient);

        // Act
        var response = await sutProvider.Sut.PostWebhook(freshdeskWebhookKey, model);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(response);
        Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);

        await mockHttpMessageHandler
            .Received(1).Send(
                Arg.Is<HttpRequestMessage>(
                    m => m.Method == HttpMethod.Post
                        && m.RequestUri.ToString().EndsWith($"{model.TicketId}/notes")
                        && m.Content.ReadAsStringAsync().Result.Contains("No user found")),
                Arg.Any<CancellationToken>());
    }


    [Theory]
    [BitAutoData((string)null, null)]
    [BitAutoData((string)null)]
    [BitAutoData(WebhookKey, null)]
    public async Task PostWebhookOnyxAi_InvalidWebhookKey_results_in_BadRequest(
        string freshdeskWebhookKey, FreshdeskOnyxAiWebhookModel model,
        BillingSettings billingSettings, SutProvider<FreshdeskController> sutProvider)
    {
        sutProvider.GetDependency<IOptions<BillingSettings>>()
            .Value.FreshDesk.WebhookKey.Returns(billingSettings.FreshDesk.WebhookKey);

        var response = await sutProvider.Sut.PostWebhookOnyxAi(freshdeskWebhookKey, model);

        var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCodeResult.StatusCode);
    }

    [Theory]
    [BitAutoData(WebhookKey)]
    public async Task PostWebhookOnyxAi_invalid_onyx_response_results_in_BadRequest(
        string freshdeskWebhookKey, FreshdeskOnyxAiWebhookModel model,
        SutProvider<FreshdeskController> sutProvider)
    {
        var billingSettings = sutProvider.GetDependency<IOptions<BillingSettings>>().Value;
        billingSettings.FreshDesk.WebhookKey.Returns(freshdeskWebhookKey);
        billingSettings.Onyx.BaseUrl.Returns("http://simulate-onyx-api.com/api");

        // mocking freshdesk Api request for ticket info
        var mockFreshdeskHttpMessageHandler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        var freshdeskHttpClient = new HttpClient(mockFreshdeskHttpMessageHandler);

        // mocking Onyx api response given a ticket description
        var mockOnyxHttpMessageHandler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        var mockOnyxResponse = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        mockOnyxHttpMessageHandler.Send(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
           .Returns(mockOnyxResponse);
        var onyxHttpClient = new HttpClient(mockOnyxHttpMessageHandler);

        sutProvider.GetDependency<IHttpClientFactory>().CreateClient("FreshdeskApi").Returns(freshdeskHttpClient);
        sutProvider.GetDependency<IHttpClientFactory>().CreateClient("OnyxApi").Returns(onyxHttpClient);

        var response = await sutProvider.Sut.PostWebhookOnyxAi(freshdeskWebhookKey, model);

        var result = Assert.IsAssignableFrom<BadRequestObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Theory]
    [BitAutoData(WebhookKey)]
    public async Task PostWebhookOnyxAi_success(
        string freshdeskWebhookKey, FreshdeskOnyxAiWebhookModel model,
        OnyxAnswerWithCitationResponseModel onyxResponse,
        SutProvider<FreshdeskController> sutProvider)
    {
        var billingSettings = sutProvider.GetDependency<IOptions<BillingSettings>>().Value;
        billingSettings.FreshDesk.WebhookKey.Returns(freshdeskWebhookKey);
        billingSettings.Onyx.BaseUrl.Returns("http://simulate-onyx-api.com/api");

        // mocking freshdesk api add note request (POST)
        var mockFreshdeskHttpMessageHandler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        var mockFreshdeskAddNoteResponse = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        mockFreshdeskHttpMessageHandler.Send(
                Arg.Is<HttpRequestMessage>(_ => _.Method == HttpMethod.Post),
                Arg.Any<CancellationToken>())
            .Returns(mockFreshdeskAddNoteResponse);
        var freshdeskHttpClient = new HttpClient(mockFreshdeskHttpMessageHandler);


        // mocking Onyx api response given a ticket description
        var mockOnyxHttpMessageHandler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        onyxResponse.ErrorMsg = string.Empty;
        var mockOnyxResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(onyxResponse))
        };
        mockOnyxHttpMessageHandler.Send(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
           .Returns(mockOnyxResponse);
        var onyxHttpClient = new HttpClient(mockOnyxHttpMessageHandler);

        sutProvider.GetDependency<IHttpClientFactory>().CreateClient("FreshdeskApi").Returns(freshdeskHttpClient);
        sutProvider.GetDependency<IHttpClientFactory>().CreateClient("OnyxApi").Returns(onyxHttpClient);

        var response = await sutProvider.Sut.PostWebhookOnyxAi(freshdeskWebhookKey, model);

        var result = Assert.IsAssignableFrom<OkResult>(response);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
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
