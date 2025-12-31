using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Premium.Commands;

public class UpgradePremiumToOrganizationCommandTests
{
    // Concrete test implementation of the abstract Plan record
    private record TestPlan : Core.Models.StaticStore.Plan
    {
        public TestPlan(
            PlanType planType,
            string? stripePlanId = null,
            string? stripeSeatPlanId = null,
            string? stripePremiumAccessPlanId = null,
            string? stripeStoragePlanId = null)
        {
            Type = planType;
            ProductTier = ProductTierType.Teams;
            Name = "Test Plan";
            IsAnnual = true;
            NameLocalizationKey = "";
            DescriptionLocalizationKey = "";
            CanBeUsedByBusiness = true;
            TrialPeriodDays = null;
            HasSelfHost = false;
            HasPolicies = false;
            HasGroups = false;
            HasDirectory = false;
            HasEvents = false;
            HasTotp = false;
            Has2fa = false;
            HasApi = false;
            HasSso = false;
            HasOrganizationDomains = false;
            HasKeyConnector = false;
            HasScim = false;
            HasResetPassword = false;
            UsersGetPremium = false;
            HasCustomPermissions = false;
            UpgradeSortOrder = 0;
            DisplaySortOrder = 0;
            LegacyYear = null;
            Disabled = false;
            PasswordManager = new PasswordManagerPlanFeatures
            {
                StripePlanId = stripePlanId,
                StripeSeatPlanId = stripeSeatPlanId,
                StripePremiumAccessPlanId = stripePremiumAccessPlanId,
                StripeStoragePlanId = stripeStoragePlanId,
                BasePrice = 0,
                SeatPrice = 0,
                ProviderPortalSeatPrice = 0,
                AllowSeatAutoscale = true,
                HasAdditionalSeatsOption = true,
                BaseSeats = 1,
                HasPremiumAccessOption = !string.IsNullOrEmpty(stripePremiumAccessPlanId),
                PremiumAccessOptionPrice = 0,
                MaxSeats = null,
                BaseStorageGb = 1,
                HasAdditionalStorageOption = !string.IsNullOrEmpty(stripeStoragePlanId),
                AdditionalStoragePricePerGb = 0,
                MaxCollections = null
            };
            SecretsManager = null;
        }
    }

    private static Core.Models.StaticStore.Plan CreateTestPlan(
        PlanType planType,
        string? stripePlanId = null,
        string? stripeSeatPlanId = null,
        string? stripePremiumAccessPlanId = null,
        string? stripeStoragePlanId = null)
    {
        return new TestPlan(planType, stripePlanId, stripeSeatPlanId, stripePremiumAccessPlanId, stripeStoragePlanId);
    }


    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly ILogger<UpgradePremiumToOrganizationCommand> _logger = Substitute.For<ILogger<UpgradePremiumToOrganizationCommand>>();
    private readonly UpgradePremiumToOrganizationCommand _command;

    public UpgradePremiumToOrganizationCommandTests()
    {
        _command = new UpgradePremiumToOrganizationCommand(
            _logger,
            _pricingClient,
            _stripeAdapter,
            _subscriberService,
            _userService);
    }

    [Theory, BitAutoData]
    public async Task Run_UserNotPremium_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = false;

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, false, null, null);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have an active Premium subscription.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_UserNoGatewaySubscriptionId_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = null;

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, false, null, null);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have a Stripe subscription.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_UserEmptyGatewaySubscriptionId_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "";

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, false, null, null);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have a Stripe subscription.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_SeatsLessThanOne_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 0, false, null, null);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Seats must be at least 1.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_TrialEndDateInPast_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        var pastDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, false, null, pastDate);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Trial end date cannot be in the past.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_PlanDoesNotSupportPremiumAccess_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";

        var mockSubscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new SubscriptionItem
                    {
                        Id = "si_123",
                        Price = new Price { Id = "price_premium" }
                    }
                }
            }
        };

        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually",
            stripePremiumAccessPlanId: null // No premium access support
        );

        _subscriberService.GetSubscriptionOrThrow(user, Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, true, null, null);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("The selected plan does not support premium access.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_PlanDoesNotSupportStorage_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";

        var mockSubscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new SubscriptionItem
                    {
                        Id = "si_123",
                        Price = new Price { Id = "price_premium" }
                    }
                }
            }
        };

        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually",
            stripeStoragePlanId: null // No storage support
        );

        _subscriberService.GetSubscriptionOrThrow(user, Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, false, 10, null);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("The selected plan does not support additional storage.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_SuccessfulUpgrade_SeatBasedPlan_ReturnsSuccess(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.Id = Guid.NewGuid();

        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var mockSubscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new SubscriptionItem
                    {
                        Id = "si_premium",
                        Price = new Price { Id = "price_premium" },
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually",
            stripePremiumAccessPlanId: "teams-premium-access-annually",
            stripeStoragePlanId: "storage-plan-teams"
        );

        _subscriberService.GetSubscriptionOrThrow(user, Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, true, 10, null);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 4 && // 1 deleted + 1 seat + 1 premium + 1 storage
                opts.Items.Any(i => i.Deleted == true) &&
                opts.Items.Any(i => i.Price == "teams-seat-annually" && i.Quantity == 5) &&
                opts.Items.Any(i => i.Price == "teams-premium-access-annually" && i.Quantity == 1) &&
                opts.Items.Any(i => i.Price == "storage-plan-teams" && i.Quantity == 10)));

        await _userService.Received(1).SaveUserAsync(user);
        Assert.Equal(currentPeriodEnd, user.PremiumExpirationDate);
    }

    [Theory, BitAutoData]
    public async Task Run_SuccessfulUpgrade_NonSeatBasedPlan_ReturnsSuccess(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";

        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var mockSubscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new SubscriptionItem
                    {
                        Id = "si_premium",
                        Price = new Price { Id = "price_premium" },
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPlan = CreateTestPlan(
            PlanType.FamiliesAnnually,
            stripePlanId: "families-plan-annually",
            stripeSeatPlanId: null // Non-seat-based
        );

        _subscriberService.GetSubscriptionOrThrow(user, Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, PlanType.FamiliesAnnually, 5, false, null, null);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 2 && // 1 deleted + 1 plan
                opts.Items.Any(i => i.Deleted == true) &&
                opts.Items.Any(i => i.Price == "families-plan-annually" && i.Quantity == 1)));

        await _userService.Received(1).SaveUserAsync(user);
    }

    [Theory, BitAutoData]
    public async Task Run_WithTrialEndDate_SetsTrialEndOnSubscription(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";

        var trialEndDate = DateTime.UtcNow.AddDays(14);
        var mockSubscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new SubscriptionItem
                    {
                        Id = "si_premium",
                        Price = new Price { Id = "price_premium" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually"
        );

        _subscriberService.GetSubscriptionOrThrow(user, Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, false, null, trialEndDate);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.TrialEnd == trialEndDate));

        Assert.Equal(trialEndDate, user.PremiumExpirationDate);
    }

    [Theory, BitAutoData]
    public async Task Run_AddsMetadataWithOriginalPremiumPriceId(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";

        var mockSubscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new SubscriptionItem
                    {
                        Id = "si_premium",
                        Price = new Price { Id = "original_premium_price_id" },
                        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
                    }
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.Id.ToString()
            }
        };

        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually"
        );

        _subscriberService.GetSubscriptionOrThrow(user, Arg.Any<SubscriptionGetOptions>())
            .Returns(mockSubscription);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, PlanType.TeamsAnnually, 5, false, null, null);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Metadata.ContainsKey("premium_upgrade_metadata") &&
                opts.Metadata["premium_upgrade_metadata"] == "original_premium_price_id" &&
                opts.Metadata.ContainsKey("userId"))); // Preserves existing metadata
    }
}
