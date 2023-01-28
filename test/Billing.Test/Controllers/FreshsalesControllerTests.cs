using Bit.Billing.Controllers;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Billing.Test.Controllers;

public class FreshsalesControllerTests
{
    private const string ApiKey = "TEST_FRESHSALES_APIKEY";
    private const string TestLead = "TEST_FRESHSALES_TESTLEAD";

    private static (FreshsalesController, IUserRepository, IOrganizationRepository) CreateSut(
        string freshsalesApiKey)
    {
        var userRepository = Substitute.For<IUserRepository>();
        var organizationRepository = Substitute.For<IOrganizationRepository>();

        var billingSettings = Options.Create(new BillingSettings
        {
            FreshsalesApiKey = freshsalesApiKey,
        });
        var globalSettings = new GlobalSettings();
        globalSettings.BaseServiceUri.Admin = "https://test.com";

        var sut = new FreshsalesController(
            userRepository,
            organizationRepository,
            billingSettings,
            Substitute.For<ILogger<FreshsalesController>>(),
            globalSettings
        );

        return (sut, userRepository, organizationRepository);
    }

    [RequiredEnvironmentTheory(ApiKey, TestLead), EnvironmentData(ApiKey, TestLead)]
    public async Task PostWebhook_Success(string freshsalesApiKey, long leadId)
    {
        // This test is only for development to use:
        // `export TEST_FRESHSALES_APIKEY=[apikey]`
        // `export TEST_FRESHSALES_TESTLEAD=[lead id]`
        // `dotnet test --filter "FullyQualifiedName~FreshsalesControllerTests.PostWebhook_Success"`
        var (sut, userRepository, organizationRepository) = CreateSut(freshsalesApiKey);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@email.com",
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
                    Name = "Test Org",
                }
            });

        var response = await sut.PostWebhook(freshsalesApiKey, new CustomWebhookRequestModel
        {
            LeadId = leadId,
        }, new CancellationToken(false));

        var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(response);
        Assert.Equal(StatusCodes.Status204NoContent, statusCodeResult.StatusCode);
    }
}
