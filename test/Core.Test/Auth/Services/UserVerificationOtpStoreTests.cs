#nullable enable

using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Services;

public class UserVerificationOtpStoreTests
{
    private const string TokenProviderName = "UserVerification";
    private const string Purpose = "EmailOtp";

    private readonly IOtpTokenProvider<DefaultOtpTokenProviderOptions> _otpTokenProvider =
        Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();

    private readonly UserVerificationOtpStore _sut;

    public UserVerificationOtpStoreTests()
    {
        _otpTokenProvider
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns("123456");
        _sut = new UserVerificationOtpStore(_otpTokenProvider);
    }

    [Theory, BitAutoData]
    public async Task IssueAsync_GeneratesOtpBoundToUserEmail(User user)
    {
        var result = await _sut.IssueAsync(user);

        Assert.Equal("123456", result);
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
    public async Task IssueAsync_ProviderGeneratesNoOtp_Throws(User user)
    {
        _otpTokenProvider
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns((string?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.IssueAsync(user));
    }
}
