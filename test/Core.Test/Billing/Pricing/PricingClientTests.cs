using System.Net;
using Bit.Core.Billing;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Services;
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
    #region GetLookupKey Tests (via GetPlan)

    [Fact]
    public async Task GetPlan_WithFamiliesAnnually2025AndFeatureFlagEnabled_UsesFamilies2025LookupKey()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var planJson = CreatePlanJson("families-2025", "Families 2025", "families", 40M, "price_id");

        mockHttp.Expect(HttpMethod.Get, "https://test.com/plans/organization/families-2025")
            .Respond("application/json", planJson);

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually2025);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PlanType.FamiliesAnnually2025, result.Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPlan_WithFamiliesAnnually2025AndFeatureFlagDisabled_UsesFamiliesLookupKey()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var planJson = CreatePlanJson("families", "Families", "families", 40M, "price_id");

        mockHttp.Expect(HttpMethod.Get, "https://test.com/plans/organization/families")
            .Respond("application/json", planJson);

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(false);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually2025);

        // Assert
        Assert.NotNull(result);
        // PreProcessFamiliesPreMigrationPlan should change "families" to "families-2025" when FF is disabled
        Assert.Equal(PlanType.FamiliesAnnually2025, result.Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    #endregion

    #region PreProcessFamiliesPreMigrationPlan Tests (via GetPlan)

    [Fact]
    public async Task GetPlan_WithFamiliesAnnually2025AndFeatureFlagDisabled_ReturnsFamiliesAnnually2025PlanType()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        // billing-pricing returns "families" lookup key because the flag is off
        var planJson = CreatePlanJson("families", "Families", "families", 40M, "price_id");

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(false);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually2025);

        // Assert
        Assert.NotNull(result);
        // PreProcessFamiliesPreMigrationPlan should convert the families lookup key to families-2025
        // and the PlanAdapter should assign the correct FamiliesAnnually2025 plan type
        Assert.Equal(PlanType.FamiliesAnnually2025, result.Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPlan_WithFamiliesAnnually2025AndFeatureFlagEnabled_ReturnsFamiliesAnnually2025PlanType()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var planJson = CreatePlanJson("families-2025", "Families", "families", 40M, "price_id");

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually2025);

        // Assert
        Assert.NotNull(result);
        // PreProcessFamiliesPreMigrationPlan should ignore the lookup key because the flag is on
        // and the PlanAdapter should assign the correct FamiliesAnnually2025 plan type
        Assert.Equal(PlanType.FamiliesAnnually2025, result.Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetPlan_WithFamiliesAnnuallyAndFeatureFlagEnabled_ReturnsFamiliesAnnuallyPlanType()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var planJson = CreatePlanJson("families", "Families", "families", 40M, "price_id");

        mockHttp.When(HttpMethod.Get, "*/plans/organization/*")
            .Respond("application/json", planJson);

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.GetPlan(PlanType.FamiliesAnnually);

        // Assert
        Assert.NotNull(result);
        // PreProcessFamiliesPreMigrationPlan should ignore the lookup key because the flag is on
        // and the PlanAdapter should assign the correct FamiliesAnnually plan type
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

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(false);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

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
    public async Task ListPlans_WithFeatureFlagDisabled_ReturnsListWithPreProcessing()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        // biling-pricing would return "families" because the flag is disabled
        var plansJson = $@"[
            {CreatePlanJson("families", "Families", "families", 40M, "price_id")},
            {CreatePlanJson("enterprise-annually", "Enterprise", "enterprise", 144M, "price_id")}
        ]";

        mockHttp.When(HttpMethod.Get, "*/plans/organization")
            .Respond("application/json", plansJson);

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(false);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.ListPlans();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // First plan should have been preprocessed from "families" to "families-2025"
        Assert.Equal(PlanType.FamiliesAnnually2025, result[0].Type);
        // Second plan should remain unchanged
        Assert.Equal(PlanType.EnterpriseAnnually, result[1].Type);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task ListPlans_WithFeatureFlagEnabled_ReturnsListWithoutPreProcessing()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var plansJson = $@"[
            {CreatePlanJson("families", "Families", "families", 40M, "price_id")}
        ]";

        mockHttp.When(HttpMethod.Get, "*/plans/organization")
            .Respond("application/json", plansJson);

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

        // Act
        var result = await pricingClient.ListPlans();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        // Plan should remain as FamiliesAnnually when FF is enabled
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

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

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

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3).Returns(true);

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

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

        var featureService = Substitute.For<IFeatureService>();

        var globalSettings = new GlobalSettings { SelfHosted = false };

        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://test.com/")
        };

        var logger = Substitute.For<ILogger<PricingClient>>();
        var pricingClient = new PricingClient(featureService, globalSettings, httpClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<BillingException>(() =>
            pricingClient.ListPlans());
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
}
