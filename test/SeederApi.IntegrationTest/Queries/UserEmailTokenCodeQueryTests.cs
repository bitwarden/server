using System.Text;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Seeder.Queries;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Queries;

/// <summary>
/// Runs the query against codes written by the real generating code, over a real in-memory
/// <see cref="IDistributedCache"/>. The query rebuilds cache keys and the new device verification envelope
/// from its own constants, so only a round trip through the actual writer can show those still agree — a
/// test that seeds the cache by hand would pass against a key nobody writes.
/// </summary>
public class UserEmailTokenCodeQueryTests
{
    private const string DeviceIdentifier = "device-identifier";

    [Fact]
    public async Task Execute_NewDeviceVerification_ReturnsCodeAndBoundDevice()
    {
        var (query, cache, user) = Arrange();
        var issuedCode = await BuildOtpStore(cache).IssueAsync(user, DeviceIdentifier);

        var response = await query.Execute(new UserEmailTokenCodeQuery.Request
        {
            Email = user.Email,
            CodeType = UserEmailTokenCodeQuery.CodeType.NewDeviceVerification,
        });

        Assert.True(response.Found);
        Assert.Equal(issuedCode, response.Code);
        Assert.Equal(DeviceIdentifier, response.DeviceIdentifier);
    }

    /// <summary>
    /// Reading a code must not consume it, or the flow under test would be unable to redeem the code the
    /// automation just fetched.
    /// </summary>
    [Fact]
    public async Task Execute_NewDeviceVerification_LeavesTheCodeRedeemable()
    {
        var (query, cache, user) = Arrange();
        var store = BuildOtpStore(cache);
        await store.IssueAsync(user, DeviceIdentifier);

        var response = await query.Execute(new UserEmailTokenCodeQuery.Request
        {
            Email = user.Email,
            CodeType = UserEmailTokenCodeQuery.CodeType.NewDeviceVerification,
        });

        Assert.True(await store.ValidateAndConsumeAsync(user, DeviceIdentifier, response.Code));
    }

    [Fact]
    public async Task Execute_NewDeviceVerification_NoCodeIssued_ReturnsNotFound()
    {
        var (query, _, user) = Arrange();

        var response = await query.Execute(new UserEmailTokenCodeQuery.Request
        {
            Email = user.Email,
            CodeType = UserEmailTokenCodeQuery.CodeType.NewDeviceVerification,
        });

        Assert.False(response.Found);
        Assert.Null(response.Code);
        Assert.Null(response.DeviceIdentifier);
    }

    /// <summary>
    /// An entry that is not the expected envelope is reported as absent rather than thrown, so a cache
    /// holding an unrecognized value does not turn every lookup into a query failure.
    /// </summary>
    [Fact]
    public async Task Execute_NewDeviceVerification_UnrecognizedCacheValue_ReturnsNotFound()
    {
        var (query, cache, user) = Arrange();
        await cache.SetAsync(
            $"NewDeviceVerification_NewDeviceVerificationCode_{user.Id}_{user.SecurityStamp}",
            Encoding.UTF8.GetBytes("123456"));

        var response = await query.Execute(new UserEmailTokenCodeQuery.Request
        {
            Email = user.Email,
            CodeType = UserEmailTokenCodeQuery.CodeType.NewDeviceVerification,
        });

        Assert.False(response.Found);
        Assert.Null(response.Code);
    }

    /// <summary>
    /// The user-verification OTP is written as a bare string under a different key, and stays that way. This
    /// pins that the envelope handling added for new device verification did not change it.
    /// </summary>
    [Fact]
    public async Task Execute_UserVerification_ReturnsBareCachedCode()
    {
        var (query, cache, user) = Arrange();
        await cache.SetAsync(
            $"EmailToken_{user.Id}_{user.SecurityStamp}_otp:{user.Email}",
            Encoding.UTF8.GetBytes("123456"));

        var response = await query.Execute(new UserEmailTokenCodeQuery.Request
        {
            Email = user.Email,
            CodeType = UserEmailTokenCodeQuery.CodeType.UserVerification,
        });

        Assert.True(response.Found);
        Assert.Equal("123456", response.Code);
        Assert.Null(response.DeviceIdentifier);
    }

    [Fact]
    public async Task Execute_UnknownUser_ReturnsNotFound()
    {
        var userRepository = Substitute.For<IUserRepository>();
        userRepository.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        var query = new UserEmailTokenCodeQuery(userRepository, BuildCache());

        var response = await query.Execute(new UserEmailTokenCodeQuery.Request
        {
            Email = "absent@bitwarden.com",
            CodeType = UserEmailTokenCodeQuery.CodeType.NewDeviceVerification,
        });

        Assert.False(response.Found);
    }

    private static (UserEmailTokenCodeQuery Query, IDistributedCache Cache, User User) Arrange()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@bitwarden.com",
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        var userRepository = Substitute.For<IUserRepository>();
        userRepository.GetByEmailAsync(user.Email).Returns(user);

        var cache = BuildCache();
        return (new UserEmailTokenCodeQuery(userRepository, cache), cache, user);
    }

    private static IDistributedCache BuildCache()
    {
        return new MemoryDistributedCache(
            new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));
    }

    private static NewDeviceVerificationOtpStore BuildOtpStore(IDistributedCache cache)
    {
        var otpTokenProvider = new OtpTokenProvider<DefaultOtpTokenProviderOptions>(
            cache, new OptionsWrapper<DefaultOtpTokenProviderOptions>(new DefaultOtpTokenProviderOptions()));

        return new NewDeviceVerificationOtpStore(otpTokenProvider, cache);
    }
}
