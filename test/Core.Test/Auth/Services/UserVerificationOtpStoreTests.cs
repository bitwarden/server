#nullable enable

using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Services;

public class UserVerificationOtpStoreTests
{
    private const string TokenProviderName = "UserVerification";
    private const string Purpose = "EmailOtp";
    private const int MaxRequestsPerWindow = 10;
    private static readonly TimeSpan _requestWindow = TimeSpan.FromMinutes(30);

    private readonly IOtpTokenProvider<DefaultOtpTokenProviderOptions> _otpTokenProvider =
        Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();

    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    // Starts at the real time, because the in-memory cache rejects an absolute expiry that is already past.
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    private readonly UserVerificationOtpStore _sut;

    public UserVerificationOtpStoreTests()
    {
        _otpTokenProvider
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns("123456");
        _sut = new UserVerificationOtpStore(_otpTokenProvider, _cache, _timeProvider);
    }

    [Theory, BitAutoData]
    public async Task IssueAsync_GeneratesOtpBoundToUserEmail(User user)
    {
        var result = await _sut.IssueAsync(user);

        Assert.Equal("123456", Assert.IsType<IssueOtpResult.Issued>(result).Otp);
        await _otpTokenProvider.Received(1).GenerateTokenAsync(
            TokenProviderName, Purpose, $"{user.Id}_{user.SecurityStamp}", user.Email);
    }

    [Theory, BitAutoData]
    public async Task ValidateAndConsumeAsync_ValidatesWithoutFailureLimit(User user)
    {
        _otpTokenProvider
            .ValidateTokenAsync("123456", TokenProviderName, Purpose, $"{user.Id}_{user.SecurityStamp}", user.Email)
            .Returns(true);

        var result = await _sut.ValidateAndConsumeAsync(user, "123456");

        Assert.True(result);
        await _otpTokenProvider.DidNotReceiveWithAnyArgs().ValidateTokenWithFailureLimitAsync(
            default!, default!, default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task ValidateAndConsumeAsync_NullOtp_ValidatesEmptyString(User user)
    {
        await _sut.ValidateAndConsumeAsync(user, null);

        await _otpTokenProvider.Received(1).ValidateTokenAsync(
            "", TokenProviderName, Purpose, $"{user.Id}_{user.SecurityStamp}", user.Email);
    }

    [Theory, BitAutoData]
    public async Task ValidateAndConsumeWithFailureLimitAsync_ValidatesWithLimitOfFive(User user)
    {
        _otpTokenProvider
            .ValidateTokenWithFailureLimitAsync(
                "123456", TokenProviderName, Purpose, $"{user.Id}_{user.SecurityStamp}", 5, user.Email)
            .Returns(true);

        var result = await _sut.ValidateAndConsumeWithFailureLimitAsync(user, "123456");

        Assert.True(result);
        await _otpTokenProvider.DidNotReceiveWithAnyArgs().ValidateTokenAsync(
            default!, default!, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task ValidateAndConsumeWithFailureLimitAsync_NullOtp_ValidatesEmptyString(User user)
    {
        await _sut.ValidateAndConsumeWithFailureLimitAsync(user, null);

        await _otpTokenProvider.Received(1).ValidateTokenWithFailureLimitAsync(
            "", TokenProviderName, Purpose, $"{user.Id}_{user.SecurityStamp}", 5, user.Email);
    }

    [Theory, BitAutoData]
    public async Task IssueAsync_RequestLimitReached_ReturnsLimitReachedAndDoesNotGenerate(User user)
    {
        await IssueUpToLimitAsync(user);
        _otpTokenProvider.ClearReceivedCalls();

        var result = await _sut.IssueAsync(user);

        Assert.IsType<IssueOtpResult.RequestLimitReached>(result);
        await _otpTokenProvider.DidNotReceiveWithAnyArgs().GenerateTokenAsync(default!, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task IssueAsync_RequestWindowEnded_IssuesAgain(User user)
    {
        await IssueUpToLimitAsync(user);
        _timeProvider.Advance(_requestWindow);

        var result = await _sut.IssueAsync(user);

        Assert.Equal("123456", Assert.IsType<IssueOtpResult.Issued>(result).Otp);
    }

    [Theory, BitAutoData]
    public async Task IssueAsync_RequestLimitReached_JustBeforeWindowEnds_ReturnsLimitReached(User user)
    {
        await IssueUpToLimitAsync(user);
        _timeProvider.Advance(_requestWindow - TimeSpan.FromSeconds(1));

        var result = await _sut.IssueAsync(user);

        Assert.IsType<IssueOtpResult.RequestLimitReached>(result);
    }

    /// <summary>
    /// Later requests in a window do not move the window's end, so steady requests cannot keep it open.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_LaterRequestsInWindow_DoNotExtendWindow(User user)
    {
        for (var i = 0; i < MaxRequestsPerWindow - 1; i++)
        {
            Assert.IsType<IssueOtpResult.Issued>(await _sut.IssueAsync(user));
        }
        _timeProvider.Advance(_requestWindow - TimeSpan.FromMinutes(1));
        Assert.IsType<IssueOtpResult.Issued>(await _sut.IssueAsync(user));
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        var result = await _sut.IssueAsync(user);

        Assert.Equal("123456", Assert.IsType<IssueOtpResult.Issued>(result).Otp);
    }

    /// <summary>
    /// The request count is kept per user, not per security stamp, so a stamp change does not reset it.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_RequestLimitReached_SecurityStampChange_StillReturnsLimitReached(User user)
    {
        await IssueUpToLimitAsync(user);
        user.SecurityStamp = Guid.NewGuid().ToString();

        var result = await _sut.IssueAsync(user);

        Assert.IsType<IssueOtpResult.RequestLimitReached>(result);
    }

    [Theory, BitAutoData]
    public async Task IssueAsync_RequestLimitReached_OtherUserStillIssues(User user, User otherUser)
    {
        await IssueUpToLimitAsync(user);

        var result = await _sut.IssueAsync(otherUser);

        Assert.Equal("123456", Assert.IsType<IssueOtpResult.Issued>(result).Otp);
    }

    [Theory, BitAutoData]
    public async Task IssueAsync_ProviderGeneratesNoOtp_Throws(User user)
    {
        _otpTokenProvider
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns((string?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.IssueAsync(user));
    }

    /// <summary>
    /// The request counter's cache entry expires when its window ends. Later requests in the window keep that
    /// expiry, so the cache removes the entry once the window is over.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_RequestCounterEntry_ExpiresAtWindowEnd(User user)
    {
        var cache = Substitute.For<IDistributedCache>();
        byte[]? storedCounter = null;
        var writeOptions = new List<DistributedCacheEntryOptions>();
        cache
            .When(c => c.SetAsync(RequestCounterKey(user), Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                storedCounter = call.ArgAt<byte[]>(1);
                writeOptions.Add(call.ArgAt<DistributedCacheEntryOptions>(2));
            });
        cache.GetAsync(RequestCounterKey(user), Arg.Any<CancellationToken>()).Returns(_ => storedCounter);
        var sut = new UserVerificationOtpStore(_otpTokenProvider, cache, _timeProvider);
        var windowEnd = _timeProvider.GetUtcNow() + _requestWindow;

        await sut.IssueAsync(user);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await sut.IssueAsync(user);

        Assert.Equal(2, writeOptions.Count);
        Assert.All(writeOptions, options =>
        {
            Assert.Equal(windowEnd, options.AbsoluteExpiration);
            Assert.Null(options.AbsoluteExpirationRelativeToNow);
            Assert.Null(options.SlidingExpiration);
        });
    }

    /// <summary>
    /// A request counter entry that is not the expected JSON shape starts a new window, and does not fail the
    /// request.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_UnreadableRequestCounterEntry_StartsNewWindow(User user)
    {
        await _cache.SetAsync(RequestCounterKey(user), "not json"u8.ToArray());

        await IssueUpToLimitAsync(user);
        var result = await _sut.IssueAsync(user);

        Assert.IsType<IssueOtpResult.RequestLimitReached>(result);
    }

    private static string RequestCounterKey(User user) => $"UserVerificationOtpRequests_{user.Id}";

    private async Task IssueUpToLimitAsync(User user)
    {
        for (var i = 0; i < MaxRequestsPerWindow; i++)
        {
            Assert.IsType<IssueOtpResult.Issued>(await _sut.IssueAsync(user));
        }
    }
}
