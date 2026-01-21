using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
            int? trialPeriodDays = null)
        {
            Type = planType;
            ProductTier = ProductTierType.Teams;
            Name = "Test Plan";
            IsAnnual = true;
            NameLocalizationKey = "";
            DescriptionLocalizationKey = "";
            CanBeUsedByBusiness = true;
            TrialPeriodDays = trialPeriodDays;
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
        string? stripeStoragePlanId = null,
        int? trialPeriodDays = null) =>
        new TestPlan(planType, stripePlanId, stripeSeatPlanId, stripePremiumAccessPlanId, stripeStoragePlanId, trialPeriodDays);

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
            CreateTestPremiumPlan("premium-annually", "personal-storage-gb-annually", available: true),
            // Legacy plan from 2020
            CreateTestPremiumPlan("premium-annually-2020", "personal-storage-gb-annually-2020", available: false)
        };
    }


    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IOrganizationUserRepository _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository = Substitute.For<IOrganizationApiKeyRepository>();
    private readonly IApplicationCacheService _applicationCacheService = Substitute.For<IApplicationCacheService>();
    private readonly ILogger<UpgradePremiumToOrganizationCommand> _logger = Substitute.For<ILogger<UpgradePremiumToOrganizationCommand>>();
    private readonly UpgradePremiumToOrganizationCommand _command;

    public UpgradePremiumToOrganizationCommandTests()
    {
        _command = new UpgradePremiumToOrganizationCommand(
            _logger,
            _pricingClient,
            _stripeAdapter,
            _userService,
            _organizationRepository,
            _organizationUserRepository,
            _organizationApiKeyRepository,
            _applicationCacheService);
    }

    [Theory, BitAutoData]
    public async Task Run_UserNotPremium_ReturnsBadRequest(User user)
    {
        // Arrange
        user.Premium = false;

        // Act
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

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
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

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
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

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
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 2 && // 1 deleted + 1 seat (no storage)
                opts.Items.Any(i => i.Deleted == true) &&
                opts.Items.Any(i => i.Price == "teams-seat-annually" && i.Quantity == 1)));

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
            stripeSeatPlanId: null // Non-seat-based
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
        var result = await _command.Run(user, "My Families Org", "encrypted-key", PlanType.FamiliesAnnually);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 2 && // 1 deleted + 1 plan
                opts.Items.Any(i => i.Deleted == true) &&
                opts.Items.Any(i => i.Price == "families-plan-annually" && i.Quantity == 1)));

        await _organizationRepository.Received(1).CreateAsync(Arg.Is<Organization>(o =>
            o.Name == "My Families Org"));
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
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Metadata.ContainsKey(StripeConstants.MetadataKeys.OrganizationId) &&
                opts.Metadata.ContainsKey(StripeConstants.MetadataKeys.PreviousPremiumPriceId) &&
                opts.Metadata[StripeConstants.MetadataKeys.PreviousPremiumPriceId] == "premium-annually" &&
                opts.Metadata.ContainsKey(StripeConstants.MetadataKeys.PreviousPeriodEndDate) &&
                opts.Metadata.ContainsKey(StripeConstants.MetadataKeys.PreviousAdditionalStorage) &&
                opts.Metadata[StripeConstants.MetadataKeys.PreviousAdditionalStorage] == "0" &&
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
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

        // Assert
        Assert.True(result.IsT0);

        // Verify that BOTH legacy items (password manager + storage) are deleted by ID
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 3 && // 2 deleted (legacy PM + legacy storage) + 1 new seat
                opts.Items.Count(i => i.Deleted == true && i.Id == "si_premium_legacy") == 1 && // Legacy PM deleted
                opts.Items.Count(i => i.Deleted == true && i.Id == "si_storage_legacy") == 1 && // Legacy storage deleted
                opts.Items.Any(i => i.Price == "teams-seat-annually" && i.Quantity == 1)));
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
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

        // Assert
        Assert.True(result.IsT0);

        // Verify that ONLY the premium password manager item is deleted (not other products)
        // Note: We delete the specific premium item by ID, so other products are untouched
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Items.Count == 2 && // 1 deleted (premium password manager) + 1 new seat
                opts.Items.Count(i => i.Deleted == true && i.Id == "si_premium") == 1 && // Premium item deleted by ID
                opts.Items.Count(i => i.Id == "si_other_product") == 0 && // Other product NOT in update (untouched)
                opts.Items.Any(i => i.Price == "teams-seat-annually" && i.Quantity == 1)));
    }

    [Theory, BitAutoData]
    public async Task Run_UserHasAdditionalStorage_CapturesStorageInMetadata(User user)
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
                        Id = "si_storage",
                        Price = new Price { Id = "personal-storage-gb-annually" },
                        Quantity = 5, // User has 5GB additional storage
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
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

        // Assert
        Assert.True(result.IsT0);

        // Verify that the additional storage quantity (5) is captured in metadata
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(opts =>
                opts.Metadata.ContainsKey(StripeConstants.MetadataKeys.PreviousAdditionalStorage) &&
                opts.Metadata[StripeConstants.MetadataKeys.PreviousAdditionalStorage] == "5" &&
                opts.Items.Count == 3 && // 2 deleted (premium + storage) + 1 new seat
                opts.Items.Count(i => i.Deleted == true) == 2));
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
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);

        // Assert
        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Premium subscription item not found.", badRequest.Response);
    }

    [Theory, BitAutoData]
    public async Task Run_PlanWithTrialPeriod_SetsTrialEnd(User user)
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

        // Create a plan with a trial period
        var mockPlan = CreateTestPlan(
            PlanType.TeamsAnnually,
            stripeSeatPlanId: "teams-seat-annually",
            trialPeriodDays: 7
        );

        // Capture the subscription update options to verify TrialEnd is set
        SubscriptionUpdateOptions capturedOptions = null;

        _stripeAdapter.GetSubscriptionAsync("sub_123")
            .Returns(mockSubscription);
        _pricingClient.ListPremiumPlans().Returns(mockPremiumPlans);
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(mockPlan);
        _stripeAdapter.UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Do<SubscriptionUpdateOptions>(opts => capturedOptions = opts))
            .Returns(Task.FromResult(mockSubscription));
        _organizationRepository.CreateAsync(Arg.Any<Organization>()).Returns(callInfo => Task.FromResult(callInfo.Arg<Organization>()));
        _organizationApiKeyRepository.CreateAsync(Arg.Any<OrganizationApiKey>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationApiKey>()));
        _organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>()).Returns(callInfo => Task.FromResult(callInfo.Arg<OrganizationUser>()));
        _applicationCacheService.UpsertOrganizationAbilityAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);
        _userService.SaveUserAsync(user).Returns(Task.CompletedTask);

        // Act
        var testStartTime = DateTime.UtcNow;
        var result = await _command.Run(user, "My Organization", "encrypted-key", PlanType.TeamsAnnually);
        var testEndTime = DateTime.UtcNow;

        // Assert
        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync("sub_123", Arg.Any<SubscriptionUpdateOptions>());

        Assert.NotNull(capturedOptions);
        Assert.NotNull(capturedOptions.TrialEnd);

        // TrialEnd is AnyOf<DateTime?, SubscriptionTrialEnd> - verify it's a DateTime
        var trialEndDateTime = capturedOptions.TrialEnd.Value as DateTime?;
        Assert.NotNull(trialEndDateTime);
        Assert.True(trialEndDateTime.Value >= testStartTime.AddDays(7));
        Assert.True(trialEndDateTime.Value <= testEndTime.AddDays(7));
    }
}
