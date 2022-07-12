using System.Text.Json;
using Bit.Billing.Controllers;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Billing.Test.Controllers
{
    public class FreshdeskControllerTests
    {
        private const string ApiKey = "TESTFRESHDESKAPIKEY";
        private const string WebhookKey = "TEST_FRESHDESK_WEBHOOKKEY";
        private const string TicketId = "TEST_FRESHDESK_TICKETID";
        private const string Email = "TEST@EMAIL.COM";

        private static (FreshdeskController, IUserRepository, IOrganizationRepository) CreateSut(
            string freshdeskApiKey)
        {
            var userRepository = Substitute.For<IUserRepository>();
            var organizationRepository = Substitute.For<IOrganizationRepository>();
            var organizationUserRepository = Substitute.For<IOrganizationUserRepository>();

            var billingSettings = Options.Create(new BillingSettings
            {
                FreshdeskApiKey = freshdeskApiKey,
                FreshdeskWebhookKey = WebhookKey
            });
            var globalSettings = new GlobalSettings();
            globalSettings.BaseServiceUri.Admin = "https://test.com";

            var mockHttpMessageHandler = Substitute.ForPartsOf<MockHttpMessageHandler>();
            var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            mockHttpMessageHandler.Send(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
               .Returns(mockResponse);
            var httpClient = new HttpClient(mockHttpMessageHandler);

            var httpClientFactory = Substitute.For<IHttpClientFactory>();
            httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

            var sut = new FreshdeskController(
                userRepository,
                organizationRepository,
                organizationUserRepository,
                billingSettings,
                Substitute.For<ILogger<FreshdeskController>>(),
                globalSettings,
                httpClientFactory
            );

            var inputData = new
            {
                ticket_id = TicketId,
                ticket_contact_email = Email,
                ticket_tags = "Billing/Account Mgmt,Org: Enterprise"
            };
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(inputData)));

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = stream;
            httpContext.Request.ContentLength = stream.Length;
            httpContext.Request.QueryString = new QueryString($"?key={WebhookKey}");

            var controllerContext = new ControllerContext()
            {
                HttpContext = httpContext,
            };

            sut.ControllerContext = controllerContext;

            return (sut, userRepository, organizationRepository);
        }

        [Fact]
        public async Task PostWebhook_Success()
        {
            // Arrange
            var (sut, userRepository, organizationRepository) = CreateSut(ApiKey);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = Email,
                Premium = true,
            };

            userRepository.GetByEmailAsync(user.Email)
                .Returns(user);

            organizationRepository.GetManyByUserIdAsync(user.Id)
                .Returns(new List<Organization>
                {
                    new Organization
                    {
                        Id = Guid.NewGuid(),
                        Name = "Test Org 1",
                    },
                    new Organization
                    {
                        Id = Guid.NewGuid(),
                        Name = "Test Org 2",
                    }
                });

            // Act
            var response = await sut.PostWebhook();

            // Assert
            var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(response);
            Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
        }

        public class MockHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Send(request, cancellationToken);
            }

            public virtual Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
