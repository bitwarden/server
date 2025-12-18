using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Subscriptions;

using static StripeConstants;

public class RestartSubscriptionCommandTests
{
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly RestartSubscriptionCommand _command;

    public RestartSubscriptionCommandTests()
    {
        _command = new RestartSubscriptionCommand(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<RestartSubscriptionCommand>>(),
            _organizationRepository,
            _pricingClient,
            _stripeAdapter,
            _subscriberService);
    }

    [Fact]
    public async Task Run_SubscriptionNotCanceled_ReturnsBadRequest()
    {
        var organization = new Organization { Id = Guid.NewGuid() };

        var subscription = new Subscription { Status = SubscriptionStatus.Active };
        _subscriberService.GetSubscription(organization).Returns(subscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Cannot restart a subscription that is not canceled.", badRequest.Response);
    }

    [Fact]
    public async Task Run_NoExistingSubscription_ReturnsBadRequest()
    {
        var organization = new Organization { Id = Guid.NewGuid() };

        _subscriberService.GetSubscription(organization).Returns((Subscription)null);

        var result = await _command.Run(organization);

        Assert.True(result.IsT1);
        var badRequest = result.AsT1;
        Assert.Equal("Cannot restart a subscription that is not canceled.", badRequest.Response);
    }

    [Fact]
    public async Task Run_Provider_ReturnsUnhandledWithNotSupportedException()
    {
        var provider = new Provider { Id = Guid.NewGuid() };

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_123"
        };

        _subscriberService.GetSubscription(provider).Returns(existingSubscription);

        var result = await _command.Run(provider);

        Assert.True(result.IsT3);
        var unhandled = result.AsT3;
        Assert.IsType<NotSupportedException>(unhandled.Exception);
    }

    [Fact]
    public async Task Run_User_ReturnsUnhandledWithNotSupportedException()
    {
        var user = new User { Id = Guid.NewGuid() };

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_123"
        };

        _subscriberService.GetSubscription(user).Returns(existingSubscription);

        var result = await _command.Run(user);

        Assert.True(result.IsT3);
        var unhandled = result.AsT3;
        Assert.IsType<NotSupportedException>(unhandled.Exception);
    }

    [Fact]
    public async Task Run_Organization_MissingPasswordManagerItem_ReturnsUnhandledWithConflictException()
    {
        var organizationId = Guid.NewGuid();
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually
        };

        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = "some-other-price-id" }, Quantity = 10 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        _pricingClient.ListPlans().Returns([plan]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT3);
        var unhandled = result.AsT3;
        Assert.IsType<ConflictException>(unhandled.Exception);
        Assert.Equal("Organization's subscription does not have a Password Manager subscription item.", unhandled.Exception.Message);
    }

    [Fact]
    public async Task Run_Organization_PlanNotFound_ReturnsUnhandledWithConflictException()
    {
        var organizationId = Guid.NewGuid();
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually
        };

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = "some-price-id" }, Quantity = 10 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        // Return a plan list that doesn't contain the organization's plan type
        _pricingClient.ListPlans().Returns([MockPlans.Get(PlanType.TeamsAnnually)]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT3);
        var unhandled = result.AsT3;
        Assert.IsType<ConflictException>(unhandled.Exception);
        Assert.Equal("Could not find plan for organization's plan type", unhandled.Exception.Message);
    }

    [Fact]
    public async Task Run_Organization_DisabledPlanWithNoEnabledReplacement_ReturnsUnhandledWithConflictException()
    {
        var organizationId = Guid.NewGuid();
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2023
        };

        var oldPlan = new DisabledEnterprisePlan2023(true);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_old",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = oldPlan.PasswordManager.StripeSeatPlanId }, Quantity = 20 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        // Return only the disabled plan, with no enabled replacement
        _pricingClient.ListPlans().Returns([oldPlan]);

        var result = await _command.Run(organization);

        Assert.True(result.IsT3);
        var unhandled = result.AsT3;
        Assert.IsType<ConflictException>(unhandled.Exception);
        Assert.Equal("Could not find the current, enabled plan for organization's tier and cadence", unhandled.Exception.Message);
    }

    [Fact]
    public async Task Run_Organization_WithNonDisabledPlan_PasswordManagerOnly_Success()
    {
        var organizationId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually
        };

        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_123",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = plan.PasswordManager.StripeSeatPlanId }, Quantity = 10 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        var newSubscription = new Subscription
        {
            Id = "sub_new",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }]
            }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        _pricingClient.ListPlans().Returns([plan]);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(options =>
            options.AutomaticTax.Enabled == true &&
            options.CollectionMethod == CollectionMethod.ChargeAutomatically &&
            options.Customer == "cus_123" &&
            options.Items.Count == 1 &&
            options.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
            options.Items[0].Quantity == 10 &&
            options.Metadata["organizationId"] == organizationId.ToString() &&
            options.OffSession == true &&
            options.TrialPeriodDays == 0));

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
            org.Id == organizationId &&
            org.GatewaySubscriptionId == "sub_new" &&
            org.Enabled == true &&
            org.ExpirationDate == currentPeriodEnd &&
            org.PlanType == PlanType.EnterpriseAnnually));
    }

    [Fact]
    public async Task Run_Organization_WithNonDisabledPlan_WithStorage_Success()
    {
        var organizationId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsAnnually
        };

        var plan = MockPlans.Get(PlanType.TeamsAnnually);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_456",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = plan.PasswordManager.StripeSeatPlanId }, Quantity = 5 },
                    new SubscriptionItem { Price = new Price { Id = plan.PasswordManager.StripeStoragePlanId }, Quantity = 3 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        var newSubscription = new Subscription
        {
            Id = "sub_new_2",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }]
            }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        _pricingClient.ListPlans().Returns([plan]);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(options =>
            options.Items.Count == 2 &&
            options.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
            options.Items[0].Quantity == 5 &&
            options.Items[1].Price == plan.PasswordManager.StripeStoragePlanId &&
            options.Items[1].Quantity == 3));

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
            org.Id == organizationId &&
            org.GatewaySubscriptionId == "sub_new_2" &&
            org.Enabled == true));
    }

    [Fact]
    public async Task Run_Organization_WithSecretsManager_Success()
    {
        var organizationId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseMonthly
        };

        var plan = MockPlans.Get(PlanType.EnterpriseMonthly);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_789",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = plan.PasswordManager.StripeSeatPlanId }, Quantity = 15 },
                    new SubscriptionItem { Price = new Price { Id = plan.PasswordManager.StripeStoragePlanId }, Quantity = 2 },
                    new SubscriptionItem { Price = new Price { Id = plan.SecretsManager.StripeSeatPlanId }, Quantity = 10 },
                    new SubscriptionItem { Price = new Price { Id = plan.SecretsManager.StripeServiceAccountPlanId }, Quantity = 100 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        var newSubscription = new Subscription
        {
            Id = "sub_new_3",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }]
            }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        _pricingClient.ListPlans().Returns([plan]);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(options =>
            options.Items.Count == 4 &&
            options.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
            options.Items[0].Quantity == 15 &&
            options.Items[1].Price == plan.PasswordManager.StripeStoragePlanId &&
            options.Items[1].Quantity == 2 &&
            options.Items[2].Price == plan.SecretsManager.StripeSeatPlanId &&
            options.Items[2].Quantity == 10 &&
            options.Items[3].Price == plan.SecretsManager.StripeServiceAccountPlanId &&
            options.Items[3].Quantity == 100));

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
            org.Id == organizationId &&
            org.GatewaySubscriptionId == "sub_new_3" &&
            org.Enabled == true));
    }

    [Fact]
    public async Task Run_Organization_WithDisabledPlan_UpgradesToNewPlan_Success()
    {
        var organizationId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually2023
        };

        var oldPlan = new DisabledEnterprisePlan2023(true);
        var newPlan = MockPlans.Get(PlanType.EnterpriseAnnually);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_old",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = oldPlan.PasswordManager.StripeSeatPlanId }, Quantity = 20 },
                    new SubscriptionItem { Price = new Price { Id = oldPlan.PasswordManager.StripeStoragePlanId }, Quantity = 5 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        var newSubscription = new Subscription
        {
            Id = "sub_upgraded",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }]
            }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        _pricingClient.ListPlans().Returns([oldPlan, newPlan]);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(options =>
            options.Items.Count == 2 &&
            options.Items[0].Price == newPlan.PasswordManager.StripeSeatPlanId &&
            options.Items[0].Quantity == 20 &&
            options.Items[1].Price == newPlan.PasswordManager.StripeStoragePlanId &&
            options.Items[1].Quantity == 5));

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
            org.Id == organizationId &&
            org.GatewaySubscriptionId == "sub_upgraded" &&
            org.Enabled == true &&
            org.PlanType == PlanType.EnterpriseAnnually &&
            org.Plan == newPlan.Name &&
            org.SelfHost == newPlan.HasSelfHost &&
            org.UsePolicies == newPlan.HasPolicies &&
            org.UseGroups == newPlan.HasGroups &&
            org.UseDirectory == newPlan.HasDirectory &&
            org.UseEvents == newPlan.HasEvents &&
            org.UseTotp == newPlan.HasTotp &&
            org.Use2fa == newPlan.Has2fa &&
            org.UseApi == newPlan.HasApi &&
            org.UseSso == newPlan.HasSso &&
            org.UseOrganizationDomains == newPlan.HasOrganizationDomains &&
            org.UseKeyConnector == newPlan.HasKeyConnector &&
            org.UseScim == newPlan.HasScim &&
            org.UseResetPassword == newPlan.HasResetPassword &&
            org.UsersGetPremium == newPlan.UsersGetPremium &&
            org.UseCustomPermissions == newPlan.HasCustomPermissions));
    }

    [Fact]
    public async Task Run_Organization_WithStorageAndSecretManagerButNoServiceAccounts_Success()
    {
        var organizationId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsAnnually
        };

        var plan = MockPlans.Get(PlanType.TeamsAnnually);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_complex",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = plan.PasswordManager.StripeSeatPlanId }, Quantity = 12 },
                    new SubscriptionItem { Price = new Price { Id = plan.PasswordManager.StripeStoragePlanId }, Quantity = 8 },
                    new SubscriptionItem { Price = new Price { Id = plan.SecretsManager.StripeSeatPlanId }, Quantity = 6 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        var newSubscription = new Subscription
        {
            Id = "sub_complex",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }]
            }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        _pricingClient.ListPlans().Returns([plan]);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(options =>
            options.Items.Count == 3 &&
            options.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
            options.Items[0].Quantity == 12 &&
            options.Items[1].Price == plan.PasswordManager.StripeStoragePlanId &&
            options.Items[1].Quantity == 8 &&
            options.Items[2].Price == plan.SecretsManager.StripeSeatPlanId &&
            options.Items[2].Quantity == 6));

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
            org.Id == organizationId &&
            org.GatewaySubscriptionId == "sub_complex" &&
            org.Enabled == true));
    }

    [Fact]
    public async Task Run_Organization_WithSecretsManagerOnly_NoServiceAccounts_Success()
    {
        var organizationId = Guid.NewGuid();
        var currentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        var organization = new Organization
        {
            Id = organizationId,
            PlanType = PlanType.TeamsMonthly
        };

        var plan = MockPlans.Get(PlanType.TeamsMonthly);

        var existingSubscription = new Subscription
        {
            Status = SubscriptionStatus.Canceled,
            CustomerId = "cus_sm",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = plan.PasswordManager.StripeSeatPlanId }, Quantity = 8 },
                    new SubscriptionItem { Price = new Price { Id = plan.SecretsManager.StripeSeatPlanId }, Quantity = 5 }
                ]
            },
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() }
        };

        var newSubscription = new Subscription
        {
            Id = "sub_sm",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }]
            }
        };

        _subscriberService.GetSubscription(organization).Returns(existingSubscription);
        _pricingClient.ListPlans().Returns([plan]);
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>()).Returns(newSubscription);

        var result = await _command.Run(organization);

        Assert.True(result.IsT0);

        await _stripeAdapter.Received(1).CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(options =>
            options.Items.Count == 2 &&
            options.Items[0].Price == plan.PasswordManager.StripeSeatPlanId &&
            options.Items[0].Quantity == 8 &&
            options.Items[1].Price == plan.SecretsManager.StripeSeatPlanId &&
            options.Items[1].Quantity == 5));

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
            org.Id == organizationId &&
            org.GatewaySubscriptionId == "sub_sm" &&
            org.Enabled == true));
    }

    private record DisabledEnterprisePlan2023 : Bit.Core.Models.StaticStore.Plan
    {
        public DisabledEnterprisePlan2023(bool isAnnual)
        {
            Type = PlanType.EnterpriseAnnually2023;
            ProductTier = ProductTierType.Enterprise;
            Name = "Enterprise (Annually) 2023";
            IsAnnual = isAnnual;
            NameLocalizationKey = "planNameEnterprise";
            DescriptionLocalizationKey = "planDescEnterprise";
            CanBeUsedByBusiness = true;
            TrialPeriodDays = 7;
            HasPolicies = true;
            HasSelfHost = true;
            HasGroups = true;
            HasDirectory = true;
            HasEvents = true;
            HasTotp = true;
            Has2fa = true;
            HasApi = true;
            HasSso = true;
            HasOrganizationDomains = true;
            HasKeyConnector = true;
            HasScim = true;
            HasResetPassword = true;
            UsersGetPremium = true;
            HasCustomPermissions = true;
            UpgradeSortOrder = 4;
            DisplaySortOrder = 4;
            LegacyYear = 2024;
            Disabled = true;

            PasswordManager = new PasswordManagerFeatures(isAnnual);
            SecretsManager = new SecretsManagerFeatures(isAnnual);
        }

        private record SecretsManagerFeatures : SecretsManagerPlanFeatures
        {
            public SecretsManagerFeatures(bool isAnnual)
            {
                BaseSeats = 0;
                BasePrice = 0;
                BaseServiceAccount = 200;
                HasAdditionalSeatsOption = true;
                HasAdditionalServiceAccountOption = true;
                AllowSeatAutoscale = true;
                AllowServiceAccountsAutoscale = true;

                if (isAnnual)
                {
                    StripeSeatPlanId = "secrets-manager-enterprise-seat-annually-2023";
                    StripeServiceAccountPlanId = "secrets-manager-service-account-2023-annually";
                    SeatPrice = 144;
                    AdditionalPricePerServiceAccount = 12;
                }
                else
                {
                    StripeSeatPlanId = "secrets-manager-enterprise-seat-monthly-2023";
                    StripeServiceAccountPlanId = "secrets-manager-service-account-2023-monthly";
                    SeatPrice = 13;
                    AdditionalPricePerServiceAccount = 1;
                }
            }
        }

        private record PasswordManagerFeatures : PasswordManagerPlanFeatures
        {
            public PasswordManagerFeatures(bool isAnnual)
            {
                BaseSeats = 0;
                BaseStorageGb = 1;
                HasAdditionalStorageOption = true;
                HasAdditionalSeatsOption = true;
                AllowSeatAutoscale = true;

                if (isAnnual)
                {
                    AdditionalStoragePricePerGb = 4;
                    StripeStoragePlanId = "storage-gb-annually";
                    StripeSeatPlanId = "2023-enterprise-org-seat-annually-old";
                    SeatPrice = 72;
                }
                else
                {
                    StripeSeatPlanId = "2023-enterprise-seat-monthly-old";
                    StripeStoragePlanId = "storage-gb-monthly";
                    SeatPrice = 7;
                    AdditionalStoragePricePerGb = 0.5M;
                }
            }
        }
    }
}
