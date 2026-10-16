using Bit.Api.AdminConsole.Partnerships;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships;

public class PartnershipRateLimitFilterTests
{
    private const int Limit = 2;

    private readonly GlobalSettings _globalSettings = new()
    {
        Partnerships = new GlobalSettings.PartnershipSettings
        {
            RateLimitRequests = Limit,
            RateLimitWindow = TimeSpan.FromMinutes(1),
        },
    };

    // 15 seconds into a one-minute window.
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 12, 0, 15, TimeSpan.Zero));

    private IPartnershipRateLimitStore _store =
        new MemoryPartnershipRateLimitStore(new MemoryCache(Options.Create(new MemoryCacheOptions())));

    private readonly Guid _partnershipId = Guid.NewGuid();

    [Fact]
    public async Task UnderLimit_Continues()
    {
        for (var i = 0; i < Limit; i++)
        {
            var (context, nextCalled) = await ExecuteAsync(_partnershipId);

            Assert.Null(context.Result);
            Assert.True(nextCalled());
        }
    }

    [Fact]
    public async Task OverLimit_Returns429WithRetryAfterUntilWindowReset()
    {
        await ExhaustBudgetAsync(_partnershipId);

        var (context, nextCalled) = await ExecuteAsync(_partnershipId);

        Assert.False(nextCalled());
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, result.StatusCode);
        var body = Assert.IsType<PartnershipErrorResponseModel>(result.Value);
        Assert.Equal("rate_limited", body.Code);
        Assert.Equal("45", context.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task SeparatePartnerships_HaveSeparateBudgets()
    {
        await ExhaustBudgetAsync(_partnershipId);

        var (context, nextCalled) = await ExecuteAsync(Guid.NewGuid());

        Assert.Null(context.Result);
        Assert.True(nextCalled());
    }

    [Fact]
    public async Task NextWindow_ResetsBudget()
    {
        await ExhaustBudgetAsync(_partnershipId);
        _timeProvider.Advance(TimeSpan.FromSeconds(45));

        var (context, nextCalled) = await ExecuteAsync(_partnershipId);

        Assert.Null(context.Result);
        Assert.True(nextCalled());
    }

    [Fact]
    public async Task StoreFailure_AllowsRequest()
    {
        _store = Substitute.For<IPartnershipRateLimitStore>();
        _store.IncrementAsync(default!, default).ThrowsAsyncForAnyArgs(new TimeoutException());

        var (context, nextCalled) = await ExecuteAsync(_partnershipId);

        Assert.Null(context.Result);
        Assert.True(nextCalled());
    }

    private async Task ExhaustBudgetAsync(Guid partnershipId)
    {
        for (var i = 0; i < Limit; i++)
        {
            await ExecuteAsync(partnershipId);
        }
    }

    private async Task<(ActionExecutingContext Context, Func<bool> NextCalled)> ExecuteAsync(Guid partnershipId)
    {
        var partnershipRequestContext = new PartnershipRequestContext();
        partnershipRequestContext.SetPartnership(new OrganizationPartnership
        {
            Id = partnershipId,
            OrganizationId = Guid.NewGuid(),
            Name = "Partner",
            Status = PartnershipStatus.Active,
        });
        var sut = new PartnershipRateLimitFilter(partnershipRequestContext, _store, _globalSettings, _timeProvider,
            NullLogger<PartnershipRateLimitFilter>.Instance);

        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(),
            controller: new object());
        var called = false;

        await sut.OnActionExecutionAsync(context, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], controller: new object()));
        });

        return (context, () => called);
    }
}
