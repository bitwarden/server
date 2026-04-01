using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;
using PremiumPurchasable = Bit.Core.Billing.Pricing.Premium.Purchasable;

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
            string? stripeStoragePlanId = null,
            int baseSeats = 1)
        {
            Type = planType;
            ProductTier = ProductTierType.Teams;
            Name = "Test Plan";
            IsAnnual = true;
            NameLocalizationKey = "";
            DescriptionLocalizationKey = "";
            CanBeUsedByBusiness = true;
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
                BaseSeats = baseSeats,
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
        string? stripeStoragePlanId = null,
        int baseSeats = 1) =>
        new TestPlan(planType, stripePlanId, stripeSeatPlanId, stripePremiumAccessPlanId, stripeStoragePlanId, baseSeats);

    private static PremiumPlan CreateTestPremiumPlan(
        string seatPriceId = "premium-annually",
        string storagePriceId = "personal-storage-gb-annually",
        bool available = true)
    {
        return new PremiumPlan
        {
            Name = "Premium",
            LegacyYear = null,
            Available = available,
            Seat = new PremiumPurchasable
            {
                StripePriceId = seatPriceId,
                Price = 10m,
                Provided = 1
            },
            Storage = new PremiumPurchasable
            {
                StripePriceId = storagePriceId,
                Price = 4m,
                Provided = 1
            }
        };
    }

    private static List<PremiumPlan> CreateTestPremiumPlansList()
    {
        return new List<PremiumPlan>
        {
            // Current available plan
            CreateTestPremiumPlan(available: true),
            // Legacy plan from 2020
            CreateTestPremiumPlan("premium-annually-2020", "personal-storage-gb-annually-2020", available: false)
        };
    }


    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IPriceIncreaseScheduler _priceIncreaseScheduler = Substitute.For<IPriceIncreaseScheduler>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IOrganizationUserRepository _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository = Substitute.For<IOrganizationApiKeyRepository>();
    private readonly ICollectionRepository _collectionRepository = Substitute.For<ICollectionRepository>();
    private readonly IApplicationCacheService _applicationCacheService = Substitute.For<IApplicationCacheService>();
    private readonly IPushNotificationService _pushNotificationService = Substitute.For<IPushNotificationService>();
    private readonly ILogger<UpgradePremiumToOrganizationCommand> _logger = Substitute.For<ILogger<UpgradePremiumToOrganizationCommand>>();
    private readonly UpgradePremiumToOrganizationCommand _command;

    public UpgradePremiumToOrganizationCommandTests()
    {
        _command = new UpgradePremiumToOrganizationCommand(
            _logger,
            _pricingClient,
            _stripeAdapter,
            _priceIncreaseScheduler,
            _userService,
            _organizationRepository,
            _organizationUserRepository,
            _organizationApiKeyRepository,
            _collectionRepository,
            _applicationCacheService,
            _pushNotificationService);
    }

    private static Core.Billing.Payment.Models.BillingAddress CreateTestBillingAddress() =>
        new() { Country = "US", PostalCode = "12345" };

    [Theory, BitAutoData]
    public async Task Run_UserNotPremium_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = false;

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

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
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have an active Premium subscription.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_UserEmptyGatewaySubscriptionId_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "";

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("User does not have an active Premium subscription.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_SuccessfulUpgrade_SeatBasedPlan_ReturnsSuccess(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";
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
                        Price = new Price { Id = "premium-annually" },
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually"
        );

        _stripeAdapter.GetSubscriptionAsync("sub_123")
            .Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 && // Only 1 item: modify existing password manager item (no storage to delete)
                opts.Items.Any(i => i.Id == "si_premium" && i.Price == "teams-seat-annually" && i.Quantity == 1 && i.Deleted != true)));

        await _organizationRepository.Received(1).CreateAsync(Arg.Is<Organization>(o =>
            o.Name == "My Organization" &&
            o.Gateway == GatewayType.Stripe &&
            o.GatewaySubscriptionId == "sub_123" &&
            o.GatewayCustomerId == "cus_123"));
        await _organizationUserRepository.Received(1).CreateAsync(Arg.Is<OrganizationUser>(ou =>
            ou.Key == "encrypted-key" &&
            ou.Status == OrganizationUserStatusType.Confirmed));
        await _organizationApiKeyRepository.Received(1).CreateAsync(Arg.Any<OrganizationApiKey>());

        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Premium == false &&
            u.GatewaySubscriptionId == null &&
            u.GatewayCustomerId == null));

    }

    [Theory, BitAutoData]
    public async Task Run_SuccessfulUpgrade_NonSeatBasedPlan_ReturnsSuccess(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" },
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(
            PlanType.FamiliesAnnually,
            stripePlanId: "families-plan-annually",
            stripeSeatPlanId: null, // Non-seat-based
            baseSeats: 6
        );

        _stripeAdapter.GetSubscriptionAsync("sub_123")
            .Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Families Org", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.FamiliesAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 && // Only 1 item: modify existing password manager item (no storage to delete)
                opts.Items.Any(i => i.Id == "si_premium" && i.Price == "families-plan-annually" && i.Quantity == 1 && i.Deleted != true)));

        await _organizationRepository.Received(1).CreateAsync(Arg.Is<Organization>(o =>
            o.Name == "My Families Org" &&
            o.Seats == 6));
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Premium == false &&
            u.GatewaySubscriptionId == null));
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
                        Price = new Price { Id = "premium-annually" },
                        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
                    }
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.Id.ToString()
            }
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually"
        );

        _stripeAdapter.GetSubscriptionAsync("sub_123")
            .Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Metadata.ContainsKey(StripeConstants.MetadataKeys.OrganizationId) &&
                opts.Metadata.ContainsKey(StripeConstants.MetadataKeys.UserId) &&
                opts.Metadata[StripeConstants.MetadataKeys.UserId] == string.Empty)); // Removes userId to unlink from User
    }

    [Theory, BitAutoData]
    public async Task Run_UserOnLegacyPremiumPlan_SuccessfullyDeletesLegacyItems(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Id = "si_premium_legacy",
                        Price = new Price { Id = "premium-annually-2020" }, // Legacy price ID
                        CurrentPeriodEnd = currentPeriodEnd
                    },
                    new SubscriptionItem
                    {
                        Id = "si_storage_legacy",
                        Price = new Price { Id = "personal-storage-gb-annually-2020" }, // Legacy storage price ID
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually"
        );

        _stripeAdapter.GetSubscriptionAsync("sub_123")
            .Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        // Verify that legacy password manager item is modified and legacy storage is deleted
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 2 && // 1 modified (legacy PM to new price) + 1 deleted (legacy storage)
                opts.Items.Count(i => i.Id == "si_premium_legacy" && i.Price == "teams-seat-annually" && i.Quantity == 1 && i.Deleted != true) == 1 && // Legacy PM modified
                opts.Items.Count(i => i.Deleted == true && i.Id == "si_storage_legacy") == 1)); // Legacy storage deleted
    }

    [Theory, BitAutoData]
    public async Task Run_UserHasPremiumPlusOtherProducts_OnlyDeletesPremiumItems(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" },
                        CurrentPeriodEnd = currentPeriodEnd
                    },
                    new SubscriptionItem
                    {
                        Id = "si_other_product",
                        Price = new Price { Id = "some-other-product-id" }, // Non-premium item
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually"
        );

        _stripeAdapter.GetSubscriptionAsync("sub_123")
            .Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        // Verify that ONLY the premium password manager item is modified (not other products)
        // Note: We modify the specific premium item by ID, so other products are untouched
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 1 && // Only modify premium password manager item
                opts.Items.Count(i => i.Id == "si_premium" && i.Price == "teams-seat-annually" && i.Quantity == 1 && i.Deleted != true) == 1 && // Premium item modified
                opts.Items.Count(i => i.Id == "si_other_product") == 0)); // Other product NOT in update (untouched)
    }

    [Theory, BitAutoData]
    public async Task Run_NoPremiumSubscriptionItemFound_ReturnsBadRequest(User user)
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
                        Id = "si_other",
                        Price = new Price { Id = "some-other-product" }, // Not a premium plan
                        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();

        _stripeAdapter.GetSubscriptionAsync("sub_123")
            .Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Premium subscription password manager item not found.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_UpdatesCustomerBillingAddress(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        var billingAddress = new Core.Billing.Payment.Models.BillingAddress { Country = "US", PostalCode = "12345" };

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _stripeAdapter.Received(1).UpdateCustomerAsync(
            "cus_123",
            Arg.Is<CustomerUpdateOptions>(opts =>
                opts.Address.Country == "US" &&
                opts.Address.PostalCode == "12345"));
    }

    [Theory, BitAutoData]
    public async Task Run_EnablesAutomaticTaxOnSubscription(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.AutomaticTax != null &&
                opts.AutomaticTax.Enabled == true));
    }

    [Theory, BitAutoData]
    public async Task Run_UsesAlwaysInvoiceProrationBehavior(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.ProrationBehavior == "always_invoice"));
    }

    [Theory, BitAutoData]
    public async Task Run_ModifiesExistingSubscriptionItem_NotDeleteAndRecreate(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        // Verify that the subscription item was modified, not deleted
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                // Should have an item with the original ID being modified
                opts.Items.Any(item =>
                    item.Id == "si_premium" &&
                    item.Price == "teams-seat-annually" &&
                    item.Quantity == 1 &&
                    item.Deleted != true)));
    }

    [Theory, BitAutoData]
    public async Task Run_CreatesOrganizationWithCorrectSettings(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _organizationRepository.Received(1).CreateAsync(
            Arg.Is<Organization>(org =>
                org.Name == "My Organization" &&
                org.BillingEmail == user.Email &&
                org.PlanType == PlanType.TeamsAnnually &&
                org.Seats == 1 &&
                org.Gateway == GatewayType.Stripe &&
                org.GatewayCustomerId == "cus_123" &&
                org.GatewaySubscriptionId == "sub_123" &&
                org.Enabled == true));
    }

    [Theory, BitAutoData]
    public async Task Run_CreatesOrganizationApiKeyWithCorrectType(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _organizationApiKeyRepository.Received(1).CreateAsync(
            Arg.Is<OrganizationApiKey>(apiKey =>
                apiKey.Type == OrganizationApiKeyType.Default &&
                !string.IsNullOrEmpty(apiKey.ApiKey)));
    }

    [Theory, BitAutoData]
    public async Task Run_CreatesOrganizationUserAsOwnerWithAllPermissions(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _organizationUserRepository.Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(orgUser =>
                orgUser.UserId == user.Id &&
                orgUser.Type == OrganizationUserType.Owner &&
                orgUser.Status == OrganizationUserStatusType.Confirmed));
    }

    [Theory, BitAutoData]
    public async Task Run_SetsOrganizationPublicAndPrivateKeys(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "test-public-key", "test-encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);

        await _organizationRepository.Received(1).CreateAsync(
            Arg.Is<Organization>(org =>
                org.PublicKey == "test-public-key" &&
                org.PrivateKey == "test-encrypted-private-key"));
    }

    [Theory, BitAutoData]
    public async Task Run_WithCollectionName_CreatesDefaultCollection(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _collectionRepository.CreateAsync(Arg.Any<Collection>(), Arg.Any<IEnumerable<CollectionAccessSelection>>(), Arg.Any<IEnumerable<CollectionAccessSelection>>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Collection>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", "Default Collection", PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _collectionRepository.Received(1).CreateAsync(
            Arg.Is<Collection>(c => c.Name == "Default Collection"),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(x => x == null),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(access =>
                access.Count() == 1 &&
                access.First().Manage == true &&
                access.First().ReadOnly == false &&
                access.First().HidePasswords == false));
    }

    [Theory, BitAutoData]
    public async Task Run_WithoutCollectionName_DoesNotCreateCollection(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        await _collectionRepository.DidNotReceive().CreateAsync(
            Arg.Any<Collection>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>>());
    }

    [Theory, BitAutoData]
    public async Task Run_CollectionCreationFails_UpgradeStillSucceeds(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Mock collection repository to throw exception
        _collectionRepository
            .When(x => x.CreateAsync(
                Arg.Any<Collection>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>(),
                Arg.Any<IEnumerable<CollectionAccessSelection>>()))
            .Do(_ => throw new InvalidOperationException("Database error"));

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", "Default Collection", PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        var organizationId = result.AsT0;
        Assert.NotEqual(Guid.Empty, organizationId);

        // Verify that core upgrade operations still completed successfully
        await _organizationRepository.Received(1).CreateAsync(Arg.Any<Organization>());
        await _organizationUserRepository.Received(1).CreateAsync(Arg.Any<OrganizationUser>());
        await _userService.Received(1).SaveUserAsync(Arg.Is<User>(u =>
            u.Premium == false &&
            u.GatewaySubscriptionId == null));

        // Verify collection creation was attempted
        await _collectionRepository.Received(1).CreateAsync(
            Arg.Any<Collection>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>>());
    }

    [Theory, BitAutoData]
    public async Task Run_WithNoTaxId_SetsTaxExemptToNone_DoesNotCreateTaxId(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        var billingAddress = new Core.Billing.Payment.Models.BillingAddress
        {
            Country = "US",
            PostalCode = "12345",
            TaxId = null
        };

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", "Default Collection", PlanType.TeamsAnnually, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateCustomerAsync(
            "cus_123",
            Arg.Is<CustomerUpdateOptions>(options =>
                options.TaxExempt == StripeConstants.TaxExempt.None));
        await _stripeAdapter.DidNotReceive().CreateTaxIdAsync(Arg.Any<string>(), Arg.Any<TaxIdCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Run_WithTaxId_SetsTaxExemptToReverse_CreatesOneTaxId(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _stripeAdapter.CreateTaxIdAsync(Arg.Any<string>(), Arg.Any<TaxIdCreateOptions>()).Returns(new TaxId());
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        var billingAddress = new Core.Billing.Payment.Models.BillingAddress
        {
            Country = "DE",
            PostalCode = "10115",
            TaxId = new Core.Billing.Payment.Models.TaxID("eu_vat", "DE123456789")
        };

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", "Default Collection", PlanType.TeamsAnnually, billingAddress);

        // Assert
        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateCustomerAsync(
            "cus_123",
            Arg.Is<CustomerUpdateOptions>(options =>
                options.TaxExempt == StripeConstants.TaxExempt.Reverse));
        await _stripeAdapter.Received(1).CreateTaxIdAsync(
            "cus_123",
            Arg.Is<TaxIdCreateOptions>(options =>
                options.Type == "eu_vat" &&
                options.Value == "DE123456789"));
    }

    [Theory, BitAutoData]
    public async Task Run_WithSpanishNIF_SetsTaxExemptToReverse_CreatesBothSpanishNIFAndEUVAT(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

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
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _stripeAdapter.CreateTaxIdAsync(Arg.Any<string>(), Arg.Any<TaxIdCreateOptions>()).Returns(new TaxId());
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        var billingAddress = new Core.Billing.Payment.Models.BillingAddress
        {
            Country = "ES",
            PostalCode = "28001",
            TaxId = new Core.Billing.Payment.Models.TaxID(StripeConstants.TaxIdType.SpanishNIF, "A12345678")
        };

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", "Default Collection", PlanType.TeamsAnnually, billingAddress);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateCustomerAsync(
            "cus_123",
            Arg.Is<CustomerUpdateOptions>(options =>
                options.TaxExempt == StripeConstants.TaxExempt.Reverse));

        // Verify Spanish NIF was created
        await _stripeAdapter.Received(1).CreateTaxIdAsync(
            "cus_123",
            Arg.Is<TaxIdCreateOptions>(options =>
                options.Type == StripeConstants.TaxIdType.SpanishNIF &&
                options.Value == "A12345678"));

        // Verify EU VAT was created with ES prefix
        await _stripeAdapter.Received(1).CreateTaxIdAsync(
            "cus_123",
            Arg.Is<TaxIdCreateOptions>(options =>
                options.Type == StripeConstants.TaxIdType.EUVAT &&
                options.Value == "ESA12345678"));
    }

    [Theory, BitAutoData]
    public async Task Run_WithSwissCountry_SetsTaxExemptToNone(User user)
    {
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        var mockSubscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new()
                    {
                        Id = "si_premium",
                        Price = new Price { Id = "premium-annually" }
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>()).Returns(mockSubscription);
        _stripeAdapter.UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>()).Returns(Task.FromResult(new Customer()));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        var billingAddress = new Core.Billing.Payment.Models.BillingAddress
        {
            Country = "CH",
            PostalCode = "8001",
            TaxId = null
        };

        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, billingAddress);

        Assert.True(result.IsT0);
        await _stripeAdapter.Received(1).UpdateCustomerAsync(
            "cus_123",
            Arg.Is<CustomerUpdateOptions>(options =>
                options.TaxExempt == StripeConstants.TaxExempt.None));
        await _stripeAdapter.DidNotReceive().CreateTaxIdAsync(Arg.Any<string>(), Arg.Any<TaxIdCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Run_ReleasesScheduleBeforeUpdate(User user)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = "sub_123";
        user.GatewayCustomerId = "cus_123";

        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var mockSubscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Id = "si_premium",
                        Price = new Price { Id = "premium-annually" },
                        CurrentPeriodEnd = currentPeriodEnd
                    }
                ]
            },
            Metadata = new Dictionary<string, string>()
        };

        var mockPremiumPlans = CreateTestPremiumPlansList();
        var mockPlan = CreateTestPlan(PlanType.TeamsAnnually, stripeSeatPlanId: "teams-seat-annually");

        _stripeAdapter.GetSubscriptionAsync("sub_123").Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(Task.FromResult(mockSubscription));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", "public-key", "encrypted-private-key", null, PlanType.TeamsAnnually, CreateTestBillingAddress());

        // Assert
        Assert.True(result.IsT0);
        await _priceIncreaseScheduler.Received(1).Release("cus_123", "sub_123");
    }
}
