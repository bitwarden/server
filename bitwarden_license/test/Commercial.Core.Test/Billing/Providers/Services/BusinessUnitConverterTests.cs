#nullable enable
using System.Text;
using Bit.Commercial.Core.Billing.Providers.Services;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Commercial.Core.Test.Billing.Providers;

public class BusinessUnitConverterTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
    private readonly GlobalSettings _globalSettings = new();
    private readonly ILogger<BusinessUnitConverter> _logger = Substitute.For<ILogger<BusinessUnitConverter>>();
    private readonly IMailService _mailService = Substitute.For<IMailService>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IOrganizationUserRepository _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IProviderOrganizationRepository _providerOrganizationRepository = Substitute.For<IProviderOrganizationRepository>();
    private readonly IProviderPlanRepository _providerPlanRepository = Substitute.For<IProviderPlanRepository>();
    private readonly IProviderRepository _providerRepository = Substitute.For<IProviderRepository>();
    private readonly IProviderUserRepository _providerUserRepository = Substitute.For<IProviderUserRepository>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    private BusinessUnitConverter BuildConverter() => new(
        _dataProtectionProvider,
        _globalSettings,
        _logger,
        _mailService,
        _organizationRepository,
        _organizationUserRepository,
        _pricingClient,
        _providerOrganizationRepository,
        _providerPlanRepository,
        _providerRepository,
        _providerUserRepository,
        _stripeAdapter,
        _subscriberService,
        _userRepository);

    #region FinalizeConversion

    [Theory, BitAutoData]
    public async Task FinalizeConversion_Succeeds_ReturnsProviderId(
        Organization organization,
        Guid userId,
        string providerKey,
        string organizationKey)
    {
        organization.PlanType = PlanType.EnterpriseAnnually2020;

        var enterpriseAnnually2020 = MockPlans.Get(PlanType.EnterpriseAnnually2020);

        var subscription = new Subscription
        {
            Id = "subscription_id",
            CustomerId = "customer_id",
            Status = StripeConstants.SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [
                    new SubscriptionItem
                    {
                        Id = "subscription_item_id",
                        Price = new Price
                        {
                            Id = enterpriseAnnually2020.PasswordManager.StripeSeatPlanId
                        }
                    }
                ]
            }
        };

        _subscriberService.GetSubscription(organization).Returns(subscription);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "provider-admin@example.com"
        };

        _userRepository.GetByIdAsync(userId).Returns(user);

        var token = SetupDataProtection(organization, user.Email);

        var organizationUser = new OrganizationUser { Status = OrganizationUserStatusType.Confirmed };

        _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(organizationUser);

        var provider = new Provider
        {
            Type = ProviderType.BusinessUnit,
            Status = ProviderStatusType.Pending
        };

        _providerRepository.GetByOrganizationIdAsync(organization.Id).Returns(provider);

        var providerUser = new ProviderUser
        {
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Invited
        };

        _providerUserRepository.GetByProviderUserAsync(provider.Id, user.Id).Returns(providerUser);

        var providerOrganization = new ProviderOrganization();

        _providerOrganizationRepository.GetByOrganizationId(organization.Id).Returns(providerOrganization);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually2020)
            .Returns(enterpriseAnnually2020);

        var enterpriseAnnually = MockPlans.Get(PlanType.EnterpriseAnnually);

        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually)
            .Returns(enterpriseAnnually);

        var businessUnitConverter = BuildConverter();

        await businessUnitConverter.FinalizeConversion(organization, userId, token, providerKey, organizationKey);

        await _stripeAdapter.Received(2).CustomerUpdateAsync(subscription.CustomerId, Arg.Any<CustomerUpdateOptions>());

        var updatedPriceId = ProviderPriceAdapter.GetActivePriceId(provider, enterpriseAnnually.Type);

        await _stripeAdapter.Received(1).SubscriptionUpdateAsync(subscription.Id, Arg.Is<SubscriptionUpdateOptions>(
            arguments =>
                arguments.Items.Count == 2 &&
                arguments.Items[0].Id == "subscription_item_id" &&
                arguments.Items[0].Deleted == true &&
                arguments.Items[1].Price == updatedPriceId &&
                arguments.Items[1].Quantity == organization.Seats));

        await _organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(arguments =>
            arguments.PlanType == PlanType.EnterpriseAnnually &&
            arguments.Status == OrganizationStatusType.Managed &&
            arguments.GatewayCustomerId == null &&
            arguments.GatewaySubscriptionId == null));

        await _providerOrganizationRepository.Received(1).ReplaceAsync(Arg.Is<ProviderOrganization>(arguments =>
            arguments.Key == organizationKey));

        await _providerRepository.Received(1).ReplaceAsync(Arg.Is<Provider>(arguments =>
            arguments.Gateway == GatewayType.Stripe &&
            arguments.GatewayCustomerId == subscription.CustomerId &&
            arguments.GatewaySubscriptionId == subscription.Id &&
            arguments.Status == ProviderStatusType.Billable));

        await _providerUserRepository.Received(1).ReplaceAsync(Arg.Is<ProviderUser>(arguments =>
            arguments.Key == providerKey &&
            arguments.Status == ProviderUserStatusType.Confirmed));
    }

    /*
     * Because the validation for finalization is not an applicative like initialization is,
     * I'm just testing one specific failure here. I don't see much value in testing every single opportunity for failure.
     */
    [Theory, BitAutoData]
    public async Task FinalizeConversion_ValidationFails_ThrowsBillingException(
        Organization organization,
        Guid userId,
        string token,
        string providerKey,
        string organizationKey)
    {
        organization.PlanType = PlanType.EnterpriseAnnually2020;

        var subscription = new Subscription
        {
            Status = StripeConstants.SubscriptionStatus.Canceled
        };

        _subscriberService.GetSubscription(organization).Returns(subscription);

        var businessUnitConverter = BuildConverter();

        await Assert.ThrowsAsync<BillingException>(() =>
            businessUnitConverter.FinalizeConversion(organization, userId, token, providerKey, organizationKey));

        await _organizationUserRepository.DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    #endregion

    #region InitiateConversion

    [Theory, BitAutoData]
    public async Task InitiateConversion_Succeeds_ReturnsProviderId(
        Organization organization,
        string providerAdminEmail)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;

        _subscriberService.GetSubscription(organization).Returns(new Subscription
        {
            Status = StripeConstants.SubscriptionStatus.Active
        });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = providerAdminEmail
        };

        _userRepository.GetByEmailAsync(providerAdminEmail).Returns(user);

        var organizationUser = new OrganizationUser { Status = OrganizationUserStatusType.Confirmed };

        _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(organizationUser);

        var provider = new Provider { Id = Guid.NewGuid() };

        _providerRepository.CreateAsync(Arg.Is<Provider>(argument =>
            argument.Name == organization.Name &&
            argument.BillingEmail == organization.BillingEmail &&
            argument.Status == ProviderStatusType.Pending &&
            argument.Type == ProviderType.BusinessUnit)).Returns(provider);

        var plan = MockPlans.Get(organization.PlanType);

        _pricingClient.GetPlanOrThrow(organization.PlanType).Returns(plan);

        var token = SetupDataProtection(organization, providerAdminEmail);

        var businessUnitConverter = BuildConverter();

        var result = await businessUnitConverter.InitiateConversion(organization, providerAdminEmail);

        Assert.True(result.IsT0);

        var providerId = result.AsT0;

        Assert.Equal(provider.Id, providerId);

        await _providerOrganizationRepository.Received(1).CreateAsync(
            Arg.Is<ProviderOrganization>(argument =>
                argument.ProviderId == provider.Id &&
                argument.OrganizationId == organization.Id));

        await _providerPlanRepository.Received(1).CreateAsync(
            Arg.Is<ProviderPlan>(argument =>
                argument.ProviderId == provider.Id &&
                argument.PlanType == PlanType.EnterpriseAnnually &&
                argument.SeatMinimum == 0 &&
                argument.PurchasedSeats == organization.Seats &&
                argument.AllocatedSeats == organization.Seats));

        await _providerUserRepository.Received(1).CreateAsync(
            Arg.Is<ProviderUser>(argument =>
                argument.ProviderId == provider.Id &&
                argument.UserId == user.Id &&
                argument.Email == user.Email &&
                argument.Status == ProviderUserStatusType.Invited &&
                argument.Type == ProviderUserType.ProviderAdmin));

        await _mailService.Received(1).SendBusinessUnitConversionInviteAsync(
            organization,
            token,
            user.Email);
    }

    [Theory, BitAutoData]
    public async Task InitiateConversion_ValidationFails_ReturnsErrors(
        Organization organization,
        string providerAdminEmail)
    {
        organization.PlanType = PlanType.TeamsMonthly;

        _subscriberService.GetSubscription(organization).Returns(new Subscription
        {
            Status = StripeConstants.SubscriptionStatus.Canceled
        });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = providerAdminEmail
        };

        _providerOrganizationRepository.GetByOrganizationId(organization.Id)
            .Returns(new ProviderOrganization());

        _userRepository.GetByEmailAsync(providerAdminEmail).Returns(user);

        var organizationUser = new OrganizationUser { Status = OrganizationUserStatusType.Invited };

        _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id)
            .Returns(organizationUser);

        var businessUnitConverter = BuildConverter();

        var result = await businessUnitConverter.InitiateConversion(organization, providerAdminEmail);

        Assert.True(result.IsT1);

        var problems = result.AsT1;

        Assert.Contains("Organization must be on an enterprise plan.", problems);

        Assert.Contains("Organization must have a valid subscription.", problems);

        Assert.Contains("Organization is already linked to a provider.", problems);

        Assert.Contains("Provider admin must be a confirmed member of the organization being converted.", problems);
    }

    #endregion

    #region ResendConversionInvite

    [Theory, BitAutoData]
    public async Task ResendConversionInvite_ConversionInProgress_Succeeds(
        Organization organization,
        string providerAdminEmail)
    {
        SetupConversionInProgress(organization, providerAdminEmail);

        var token = SetupDataProtection(organization, providerAdminEmail);

        var businessUnitConverter = BuildConverter();

        await businessUnitConverter.ResendConversionInvite(organization, providerAdminEmail);

        await _mailService.Received(1).SendBusinessUnitConversionInviteAsync(
            organization,
            token,
            providerAdminEmail);
    }

    [Theory, BitAutoData]
    public async Task ResendConversionInvite_NoConversionInProgress_DoesNothing(
        Organization organization,
        string providerAdminEmail)
    {
        SetupDataProtection(organization, providerAdminEmail);

        var businessUnitConverter = BuildConverter();

        await businessUnitConverter.ResendConversionInvite(organization, providerAdminEmail);

        await _mailService.DidNotReceiveWithAnyArgs().SendBusinessUnitConversionInviteAsync(
            Arg.Any<Organization>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    #endregion

    #region ResetConversion

    [Theory, BitAutoData]
    public async Task ResetConversion_ConversionInProgress_Succeeds(
        Organization organization,
        string providerAdminEmail)
    {
        var (provider, providerOrganization, providerUser, providerPlan) = SetupConversionInProgress(organization, providerAdminEmail);

        var businessUnitConverter = BuildConverter();

        await businessUnitConverter.ResetConversion(organization, providerAdminEmail);

        await _providerOrganizationRepository.Received(1)
            .DeleteAsync(providerOrganization);

        await _providerUserRepository.Received(1)
            .DeleteAsync(providerUser);

        await _providerPlanRepository.Received(1)
            .DeleteAsync(providerPlan);

        await _providerRepository.Received(1)
            .DeleteAsync(provider);
    }

    [Theory, BitAutoData]
    public async Task ResetConversion_NoConversionInProgress_DoesNothing(
        Organization organization,
        string providerAdminEmail)
    {
        var businessUnitConverter = BuildConverter();

        await businessUnitConverter.ResetConversion(organization, providerAdminEmail);

        await _providerOrganizationRepository.DidNotReceiveWithAnyArgs()
            .DeleteAsync(Arg.Any<ProviderOrganization>());

        await _providerUserRepository.DidNotReceiveWithAnyArgs()
            .DeleteAsync(Arg.Any<ProviderUser>());

        await _providerPlanRepository.DidNotReceiveWithAnyArgs()
            .DeleteAsync(Arg.Any<ProviderPlan>());

        await _providerRepository.DidNotReceiveWithAnyArgs()
            .DeleteAsync(Arg.Any<Provider>());
    }

    #endregion

    #region Utilities

    private string SetupDataProtection(
        Organization organization,
        string providerAdminEmail)
    {
        var dataProtector = new MockDataProtector(organization, providerAdminEmail);
        _dataProtectionProvider.CreateProtector($"{nameof(BusinessUnitConverter)}DataProtector").Returns(dataProtector);
        return dataProtector.Protect(dataProtector.Token);
    }

    private (Provider, ProviderOrganization, ProviderUser, ProviderPlan) SetupConversionInProgress(
        Organization organization,
        string providerAdminEmail)
    {
        var user = new User { Id = Guid.NewGuid() };

        _userRepository.GetByEmailAsync(providerAdminEmail).Returns(user);

        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Type = ProviderType.BusinessUnit,
            Status = ProviderStatusType.Pending
        };

        _providerRepository.GetByOrganizationIdAsync(organization.Id).Returns(provider);

        var providerUser = new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            UserId = user.Id,
            Type = ProviderUserType.ProviderAdmin,
            Status = ProviderUserStatusType.Invited,
            Email = providerAdminEmail
        };

        _providerUserRepository.GetByProviderUserAsync(provider.Id, user.Id)
            .Returns(providerUser);

        var providerOrganization = new ProviderOrganization
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            ProviderId = provider.Id
        };

        _providerOrganizationRepository.GetByOrganizationId(organization.Id)
            .Returns(providerOrganization);

        var providerPlan = new ProviderPlan
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            PlanType = PlanType.EnterpriseAnnually
        };

        _providerPlanRepository.GetByProviderId(provider.Id).Returns([providerPlan]);

        return (provider, providerOrganization, providerUser, providerPlan);
    }

    #endregion
}

public class MockDataProtector(
    Organization organization,
    string providerAdminEmail) : IDataProtector
{
    public string Token = $"BusinessUnitConversionInvite {organization.Id} {providerAdminEmail} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}";

    public IDataProtector CreateProtector(string purpose) => this;

    public byte[] Protect(byte[] plaintext) => Encoding.UTF8.GetBytes(Token);

    public byte[] Unprotect(byte[] protectedData) => Encoding.UTF8.GetBytes(Token);
}
