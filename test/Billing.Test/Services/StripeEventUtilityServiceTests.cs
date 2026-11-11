using Bit.Billing.Constants;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Services;

public class StripeEventUtilityServiceTests
{
    private readonly IStripeFacade _stripeFacade = Substitute.For<IStripeFacade>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly StripeEventUtilityService _sut;

    public StripeEventUtilityServiceTests()
    {
        _sut = new StripeEventUtilityService(
            _stripeFacade,
            Substitute.For<ILogger<StripeEventUtilityService>>(),
            Substitute.For<ITransactionRepository>(),
            Substitute.For<IMailService>(),
            _userRepository,
            new GlobalSettings());
    }

    [Theory]
    [InlineData(StripeSubscriptionStatus.Active)]
    [InlineData(StripeSubscriptionStatus.Trialing)]
    [InlineData(StripeSubscriptionStatus.PastDue)]
    public async Task GetOtherLiveSubscriptionAsync_OtherSubscriptionIsLive_ReturnsIt(string status)
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_ending" };
        var ending = Subscription("sub_ending", StripeSubscriptionStatus.Canceled, user.Id);
        var other = Subscription("sub_other", status, user.Id);
        CustomerSubscriptions(ending, other);

        var result = await _sut.GetOtherLiveSubscriptionAsync(user, ending);

        Assert.Equal("sub_other", result?.Id);
    }

    [Theory]
    [InlineData(StripeSubscriptionStatus.Canceled)]
    [InlineData(StripeSubscriptionStatus.IncompleteExpired)]
    [InlineData(StripeSubscriptionStatus.Unpaid)]
    [InlineData(StripeSubscriptionStatus.Incomplete)]
    public async Task GetOtherLiveSubscriptionAsync_OtherSubscriptionIsNotLive_ReturnsNull(string status)
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_other" };
        var ending = Subscription("sub_ending", StripeSubscriptionStatus.Canceled, user.Id);
        CustomerSubscriptions(ending, Subscription("sub_other", status, user.Id));

        var result = await _sut.GetOtherLiveSubscriptionAsync(user, ending);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOtherLiveSubscriptionAsync_OnlyTheEventSubscriptionIsLive_ReturnsNull()
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_renewing" };
        var renewing = Subscription("sub_renewing", StripeSubscriptionStatus.Active, user.Id);
        CustomerSubscriptions(renewing);

        var result = await _sut.GetOtherLiveSubscriptionAsync(user, renewing);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOtherLiveSubscriptionAsync_OnlyAnotherUsersSubscriptionIsLive_ReturnsNull()
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_ending" };
        var ending = Subscription("sub_ending", StripeSubscriptionStatus.Canceled, user.Id);
        CustomerSubscriptions(ending, Subscription("sub_other", StripeSubscriptionStatus.Active, Guid.NewGuid()));

        var result = await _sut.GetOtherLiveSubscriptionAsync(user, ending);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOtherLiveSubscriptionAsync_EndingSubscriptionIsLinked_ReturnsUnlinkedLiveSubscription()
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_ending" };
        var ending = Subscription("sub_ending", StripeSubscriptionStatus.Unpaid, user.Id);
        CustomerSubscriptions(ending, Subscription("sub_unlinked", StripeSubscriptionStatus.Active, user.Id));

        var result = await _sut.GetOtherLiveSubscriptionAsync(user, ending);

        Assert.Equal("sub_unlinked", result?.Id);
    }

    [Fact]
    public async Task GetOtherLiveSubscriptionAsync_SeveralLiveSubscriptions_PrefersLinkedOne()
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_linked" };
        var ending = Subscription("sub_ending", StripeSubscriptionStatus.Canceled, user.Id);
        CustomerSubscriptions(
            ending,
            Subscription("sub_later", StripeSubscriptionStatus.Active, user.Id, DateTime.UtcNow.AddMonths(12)),
            Subscription("sub_linked", StripeSubscriptionStatus.Active, user.Id, DateTime.UtcNow.AddMonths(6)));

        var result = await _sut.GetOtherLiveSubscriptionAsync(user, ending);

        Assert.Equal("sub_linked", result?.Id);
    }

    [Fact]
    public async Task GetOtherLiveSubscriptionAsync_SeveralUnlinkedLiveSubscriptions_ReturnsLatestPeriodEnd()
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_ending" };
        var ending = Subscription("sub_ending", StripeSubscriptionStatus.Canceled, user.Id);
        CustomerSubscriptions(
            ending,
            Subscription("sub_earlier", StripeSubscriptionStatus.Active, user.Id, DateTime.UtcNow.AddMonths(6)),
            Subscription("sub_later", StripeSubscriptionStatus.Active, user.Id, DateTime.UtcNow.AddMonths(12)));

        var result = await _sut.GetOtherLiveSubscriptionAsync(user, ending);

        Assert.Equal("sub_later", result?.Id);
    }

    [Fact]
    public async Task GetOtherLiveSubscriptionAsync_ListingFails_Throws()
    {
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_other" };
        var ending = Subscription("sub_ending", StripeSubscriptionStatus.Canceled, user.Id);
        _stripeFacade.ListSubscriptions(Arg.Any<SubscriptionListOptions>()).ThrowsAsync(new StripeException("unavailable"));

        await Assert.ThrowsAsync<StripeException>(() => _sut.GetOtherLiveSubscriptionAsync(user, ending));
    }

    [Theory]
    [InlineData(StripeSubscriptionStatus.Active)]
    [InlineData(StripeSubscriptionStatus.Trialing)]
    [InlineData(StripeSubscriptionStatus.PastDue)]
    public async Task GetLiveLinkedSubscriptionAsync_LinkedSubscriptionIsLive_ReturnsIt(string status)
    {
        var organization = new Organization { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_linked" };
        var ending = OrganizationSubscription("sub_ending", StripeSubscriptionStatus.Canceled, organization.Id);
        _stripeFacade.GetSubscription("sub_linked").Returns(OrganizationSubscription("sub_linked", status, organization.Id));

        var result = await _sut.GetLiveLinkedSubscriptionAsync(organization, ending);

        Assert.Equal("sub_linked", result?.Id);
    }

    [Theory]
    [InlineData(StripeSubscriptionStatus.Canceled)]
    [InlineData(StripeSubscriptionStatus.Unpaid)]
    [InlineData(StripeSubscriptionStatus.Incomplete)]
    [InlineData(StripeSubscriptionStatus.IncompleteExpired)]
    public async Task GetLiveLinkedSubscriptionAsync_LinkedSubscriptionIsNotLive_ReturnsNull(string status)
    {
        var organization = new Organization { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_linked" };
        var ending = OrganizationSubscription("sub_ending", StripeSubscriptionStatus.Canceled, organization.Id);
        _stripeFacade.GetSubscription("sub_linked").Returns(OrganizationSubscription("sub_linked", status, organization.Id));

        var result = await _sut.GetLiveLinkedSubscriptionAsync(organization, ending);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("sub_ending")]
    [InlineData(null)]
    public async Task GetLiveLinkedSubscriptionAsync_EventSubscriptionIsLinkedOrNoneIsLinked_ReturnsNullWithoutFetching(string? linkedSubscriptionId)
    {
        var organization = new Organization { Id = Guid.NewGuid(), GatewaySubscriptionId = linkedSubscriptionId };
        var ending = OrganizationSubscription("sub_ending", StripeSubscriptionStatus.Unpaid, organization.Id);

        var result = await _sut.GetLiveLinkedSubscriptionAsync(organization, ending);

        Assert.Null(result);
        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(default!);
    }

    [Fact]
    public async Task GetLiveLinkedSubscriptionAsync_LinkedSubscriptionDoesNotExist_ReturnsNull()
    {
        var organization = new Organization { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_missing" };
        var ending = OrganizationSubscription("sub_ending", StripeSubscriptionStatus.Canceled, organization.Id);
        _stripeFacade.GetSubscription("sub_missing")
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "resource_missing" } });

        var result = await _sut.GetLiveLinkedSubscriptionAsync(organization, ending);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLiveLinkedSubscriptionAsync_FetchFails_Throws()
    {
        var organization = new Organization { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_linked" };
        var ending = OrganizationSubscription("sub_ending", StripeSubscriptionStatus.Canceled, organization.Id);
        _stripeFacade.GetSubscription("sub_linked").ThrowsAsync(new StripeException("unavailable"));

        await Assert.ThrowsAsync<StripeException>(() => _sut.GetLiveLinkedSubscriptionAsync(organization, ending));
    }

    [Fact]
    public async Task LinkUserSubscriptionAsync_DifferentSubscription_LinksItAndSetsExpiration()
    {
        var periodEnd = DateTime.UtcNow.AddMonths(12);
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_ending", PremiumExpirationDate = DateTime.UtcNow };

        await _sut.LinkUserSubscriptionAsync(user, Subscription("sub_live", StripeSubscriptionStatus.Active, user.Id, periodEnd));

        await _userRepository.Received(1).ReplaceAsync(Arg.Is<User>(u =>
            u.GatewaySubscriptionId == "sub_live" && u.PremiumExpirationDate == periodEnd));
    }

    [Fact]
    public async Task LinkUserSubscriptionAsync_AlreadyLinkedWithSameExpiration_WritesNothing()
    {
        var periodEnd = DateTime.UtcNow.AddMonths(12);
        var user = new User { Id = Guid.NewGuid(), GatewaySubscriptionId = "sub_live", PremiumExpirationDate = periodEnd };

        await _sut.LinkUserSubscriptionAsync(user, Subscription("sub_live", StripeSubscriptionStatus.Active, user.Id, periodEnd));

        await _userRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    private void CustomerSubscriptions(params Subscription[] subscriptions) =>
        _stripeFacade.ListSubscriptions(Arg.Is<SubscriptionListOptions>(options => options.Customer == "cus_1"))
            .Returns(new StripeList<Subscription> { Data = [.. subscriptions] });

    private static Subscription Subscription(string id, string status, Guid userId, DateTime? currentPeriodEnd = null) =>
        Subscription(id, status, "userId", userId, currentPeriodEnd);

    private static Subscription OrganizationSubscription(string id, string status, Guid organizationId, DateTime? currentPeriodEnd = null) =>
        Subscription(id, status, "organizationId", organizationId, currentPeriodEnd);

    private static Subscription Subscription(string id, string status, string metadataKey, Guid subscriberId, DateTime? currentPeriodEnd) => new()
    {
        Id = id,
        CustomerId = "cus_1",
        Status = status,
        Metadata = new Dictionary<string, string> { [metadataKey] = subscriberId.ToString() },
        Items = new StripeList<SubscriptionItem>
        {
            Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd ?? DateTime.UtcNow.AddMonths(12) }]
        }
    };
}
