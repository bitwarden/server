using System.Net;
using Bit.Core.Billing;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RichardSzalay.MockHttp;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Billing.Pricing;

[SutProviderCustomize]
public class PricingClientTests
{
    #region GetPlan Lookup Key Tests

    [Fact]
    public async Task GetPlan_WithFamiliesAnnually2025_UsesFamilies2025LookupKey()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var planJson = CreatePlanJson("families-2025", "Families 2025", "families", 40M, "price_id");

        mockHttp.Expect(HttpMethod.Get, "https://test.com/plans/organization/families-2025")
            .Respond("application/json", planJson);

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually2025);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PlanType.FamiliesAnnually2025, result.Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPlan_WithFamiliesAnnually2025_ReturnsFamiliesAnnually2025PlanType()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var planJson = CreatePlanJson("families-2025", "Families", "families", 40M, "price_id");

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually2025);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PlanType.FamiliesAnnually2025, result.Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPlan_WithFamiliesAnnually_ReturnsFamiliesAnnuallyPlanType()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var planJson = CreatePlanJson("families", "Families", "families", 40M, "price_id");

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PlanType.FamiliesAnnually, result.Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPlan_WithOtherLookupKey_KeepsLookupKeyUnchanged()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var planJson = CreatePlanJson("enterprise-annually", "Enterprise", "enterprise", 144M, "price_id");

        mockHttp.Expect(HttpMethod.Get, "https://test.com/plans/organization/enterprise-annually")
            .Respond("application/json", planJson);

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.EnterpriseAnnually);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PlanType.EnterpriseAnnually, result.Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    #endregion

    #region ListPlans Tests

    [Fact]
    public async Task ListPlans_ReturnsPlans()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var plansJson = $@"[
            {CreatePlanJson("families", "Families", "families", 40M, "price_id")}
        ]";

        mockHttp.When(HttpMethod.Get, "*/plans/organization")
            .Respond("application/json", plansJson);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.ListPlans();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(PlanType.FamiliesAnnually, result[0].Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    #endregion

    #region GetPlan - Additional Coverage

    [Theory, BitAutoData]
    public async Task GetPlan_WhenSelfHosted_ReturnsNull(
        SutProvider<PricingClient> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<GlobalSettings>();
        globalSettings.SelfHosted = true;

        // Act
        var result = await sutProvider.Sut.GetPlan(PlanType.FamiliesAnnually2025);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetPlan_WhenLookupKeyNotFound_ReturnsNull(
        SutProvider<PricingClient> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().SelfHosted = false;

        // Act - Using PlanType that doesn't have a lookup key mapping
        var result = await sutProvider.Sut.GetPlan(unchecked((PlanType)999));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlan_WhenPricingServiceReturnsNotFound_ReturnsNull()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond(HttpStatusCode.NotFound);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually2025);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlan_WhenPricingServiceReturnsError_ThrowsBillingException()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond(HttpStatusCode.InternalServerError);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<BillingException>(() =>
            pricingClient.GetPlan(PlanType.FamiliesAnnually2025));
    }

    #endregion

    #region ListPlans - Additional Coverage

    [Theory, BitAutoData]
    public async Task ListPlans_WhenSelfHosted_ReturnsEmptyList(
        SutProvider<PricingClient> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<GlobalSettings>();
        globalSettings.SelfHosted = true;

        // Act
        var result = await sutProvider.Sut.ListPlans();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListPlans_WhenPricingServiceReturnsError_ThrowsBillingException()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "*/plans/organization")
            .Respond(HttpStatusCode.InternalServerError);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<BillingException>(() =>
            pricingClient.ListPlans());
    }

    #endregion

    #region ListPremiumPlans Tests

    [Fact]
    public async Task ListPremiumPlans_Success_ReturnsPremiumPlans()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var plansJson = $@"[
            {CreatePremiumPlanJson("Premium", true, null, 10M, "price_premium", 4M, "price_storage", 1)},
            {CreatePremiumPlanJson("Premium Legacy", false, 2019, 10M, "price_premium_legacy", 4M, "price_storage_legacy", 1)}
        ]";

        mockHttp.When(HttpMethod.Get, "*/plans/premium")
            .Respond("application/json", plansJson);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.ListPremiumPlans();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Premium", result[0].Name);
        Assert.True(result[0].Available);
        Assert.Null(result[0].LegacyYear);
        Assert.Equal(10M, result[0].Seat.Price);
        Assert.Equal("price_premium", result[0].Seat.StripePriceId);
        Assert.Equal(4M, result[0].Storage.Price);
        Assert.Equal("price_storage", result[0].Storage.StripePriceId);
        Assert.Equal(1, result[0].Storage.Provided);
        Assert.Equal("Premium Legacy", result[1].Name);
        Assert.False(result[1].Available);
        Assert.Equal(2019, result[1].LegacyYear);
    }

    [Theory, BitAutoData]
    public async Task ListPremiumPlans_WhenSelfHosted_ReturnsEmptyList(
        SutProvider<PricingClient> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>().SelfHosted = true;

        // Act
        var result = await sutProvider.Sut.ListPremiumPlans();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListPremiumPlans_WhenPricingServiceReturnsError_ThrowsBillingException()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "*/plans/premium")
            .Respond(HttpStatusCode.InternalServerError);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<BillingException>(() =>
            pricingClient.ListPremiumPlans());
    }

    #endregion

    #region GetAvailablePremiumPlan Tests

    [Fact]
    public async Task GetAvailablePremiumPlan_WithAvailablePlan_ReturnsIt()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var plansJson = $@"[
            {CreatePremiumPlanJson("Premium Legacy", false, 2019, 10M, "price_legacy", 4M, "price_storage_legacy", 1)},
            {CreatePremiumPlanJson("Premium", true, null, 10M, "price_premium", 4M, "price_storage", 1)}
        ]";

        mockHttp.When(HttpMethod.Get, "*/plans/premium")
            .Respond("application/json", plansJson);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetAvailablePremiumPlan();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Premium", result.Name);
        Assert.True(result.Available);
    }

    [Fact]
    public async Task GetAvailablePremiumPlan_WithNoAvailablePlan_ThrowsNotFoundException()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var plansJson = $@"[
            {CreatePremiumPlanJson("Premium Legacy", false, 2019, 10M, "price_legacy", 4M, "price_storage_legacy", 1)}
        ]";

        mockHttp.When(HttpMethod.Get, "*/plans/premium")
            .Respond("application/json", plansJson);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            pricingClient.GetAvailablePremiumPlan());
    }

    [Fact]
    public async Task GetAvailablePremiumPlan_WithEmptyList_ThrowsNotFoundException()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "*/plans/premium")
            .Respond("application/json", "[]");

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(globalSettings, httpClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            pricingClient.GetAvailablePremiumPlan());
    }

    #endregion

    private static string CreatePlanJson(
        string lookupKey,
        string name,
        string tier,
        decimal seatsPrice,
        string seatsStripePriceId,
        int seatsQuantity = 1)
    {
        return $@"{{
            ""lookupKey"": ""{lookupKey}"",
            ""name"": ""{name}"",
            ""tier"": ""{tier}"",
            ""features"": [],
            ""seats"": {{
                ""type"": ""packaged"",
                ""quantity"": {seatsQuantity},
                ""price"": {seatsPrice},
                ""stripePriceId"": ""{seatsStripePriceId}""
            }},
            ""canUpgradeTo"": [],
            ""additionalData"": {{
                ""nameLocalizationKey"": ""{lookupKey}Name"",
                ""descriptionLocalizationKey"": ""{lookupKey}Description""
            }}
        }}";
    }

    private static string CreatePremiumPlanJson(
        string name,
        bool available,
        int? legacyYear,
        decimal seatPrice,
        string seatStripePriceId,
        decimal storagePrice,
        string storageStripePriceId,
        int storageProvided)
    {
        var legacyYearJson = legacyYear.HasValue ? legacyYear.Value.ToString() : "null";
        return $@"{{
            ""name"": ""{name}"",
            ""available"": {available.ToString().ToLower()},
            ""legacyYear"": {legacyYearJson},
            ""seat"": {{
                ""stripePriceId"": ""{seatStripePriceId}"",
                ""price"": {seatPrice},
                ""provided"": 0
            }},
            ""storage"": {{
                ""stripePriceId"": ""{storageStripePriceId}"",
                ""price"": {storagePrice},
                ""provided"": {storageProvided}
            }}
        }}";
    }
}
