#nullable enable

using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.UserVerification;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserVerification;

[SutProviderCustomize]
public class UserVerificationOtpCommandTests
{
    [Theory, BitAutoData]
    public async Task SendOtpAsync_IssuesAndEmailsOtp(SutProvider<UserVerificationOtpCommand> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserVerificationOtpStore>().IssueAsync(user).Returns("123456");

        await sutProvider.Sut.SendOtpAsync(user);

        await sutProvider.GetDependency<IMailService>().Received(1).SendOTPEmailAsync(user.Email, "123456");
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData((string?)null)]
    public async Task SendOtpAsync_NoEmail_ThrowsAndDoesNotIssue(
        string? email, SutProvider<UserVerificationOtpCommand> sutProvider, User user)
    {
        user.Email = email!;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SendOtpAsync(user));

        Assert.Equal("No user email.", exception.Message);
        await sutProvider.GetDependency<IUserVerificationOtpStore>().DidNotReceiveWithAnyArgs().IssueAsync(default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendOTPEmailAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task VerifyOtpAsync_ValidOtp_EmailNotVerified_SetsEmailVerifiedAndSaves(
        SutProvider<UserVerificationOtpCommand> sutProvider, User user)
    {
        user.EmailVerified = false;
        var revisionDateBefore = user.RevisionDate = DateTime.UtcNow.AddDays(-1);
        sutProvider.GetDependency<IUserVerificationOtpStore>()
            .ValidateAndConsumeWithFailureLimitAsync(user, "123456")
            .Returns(true);

        var result = await sutProvider.Sut.VerifyOtpAsync(user, "123456");

        Assert.True(result);
        Assert.True(user.EmailVerified);
        Assert.True(user.RevisionDate > revisionDateBefore);
        Assert.Equal(user.RevisionDate, user.AccountRevisionDate);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
        await sutProvider.GetDependency<IUserVerificationOtpStore>().DidNotReceiveWithAnyArgs()
            .ValidateAndConsumeAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task VerifyOtpAsync_ValidOtp_EmailAlreadyVerified_DoesNotSave(
        SutProvider<UserVerificationOtpCommand> sutProvider, User user)
    {
        user.EmailVerified = true;
        sutProvider.GetDependency<IUserVerificationOtpStore>()
            .ValidateAndConsumeWithFailureLimitAsync(user, "123456")
            .Returns(true);

        var result = await sutProvider.Sut.VerifyOtpAsync(user, "123456");

        Assert.True(result);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task VerifyOtpAsync_InvalidOtp_ReturnsFalseAndDoesNotSave(
        SutProvider<UserVerificationOtpCommand> sutProvider, User user)
    {
        user.EmailVerified = false;
        sutProvider.GetDependency<IUserVerificationOtpStore>()
            .ValidateAndConsumeWithFailureLimitAsync(user, "123456")
            .Returns(false);

        var result = await sutProvider.Sut.VerifyOtpAsync(user, "123456");

        Assert.False(result);
        Assert.False(user.EmailVerified);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }
}
