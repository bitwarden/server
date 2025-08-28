using System.Collections.Specialized;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.IdentityServer;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.SendAccess;

[SutProviderCustomize]
public class SendEmailOtpRequestValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_MissingEmail_ReturnsInvalidRequest(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId)
    {
        // Arrange
        tokenRequest.Raw = CreateValidatedTokenRequest(sendId);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal("email is required.", result.ErrorDescription);

        // Verify no OTP generation or email sending occurred
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceive()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendSendEmailOtpEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_EmailNotInList_ReturnsInvalidRequest(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        string email,
        Guid sendId)
    {
        // Arrange
        tokenRequest.Raw = CreateValidatedTokenRequest(sendId, email);
        var emailOTP = new EmailOtp(["user@test.dev"]);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal("email is invalid.", result.ErrorDescription);

        // Verify no OTP generation or email sending occurred
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceive()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendSendEmailOtpEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_EmailWithoutOtp_GeneratesAndSendsOtp(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email,
        string generatedToken)
    {
        // Arrange
        tokenRequest.Raw = CreateValidatedTokenRequest(sendId, email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        var expectedUniqueId = string.Format(SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .GenerateTokenAsync(
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId)
            .Returns(generatedToken);

        emailOtp = emailOtp with { Emails = [email] };

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal("email otp sent.", result.ErrorDescription);

        // Verify OTP generation
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .Received(1)
            .GenerateTokenAsync(
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId);

        // Verify email sending
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendSendEmailOtpEmailAsync(email, generatedToken, SendAccessConstants.OtpToken.Purpose);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_OtpGenerationFails_ReturnsGenerationFailedError(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email)
    {
        // Arrange
        tokenRequest.Raw = CreateValidatedTokenRequest(sendId, email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        emailOtp = emailOtp with { Emails = [email] };

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((string)null); // Generation fails

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);

        // Verify no email was sent
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendSendEmailOtpEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_ValidOtp_ReturnsSuccess(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email,
        string otp)
    {
        // Arrange
        tokenRequest.Raw = CreateValidatedTokenRequest(sendId, email, otp);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        emailOtp = emailOtp with { Emails = [email] };

        var expectedUniqueId = string.Format(SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .ValidateTokenAsync(
                otp,
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.False(result.IsError);
        var sub = result.Subject;
        Assert.Equal(sendId.ToString(), sub.Claims.First(c => c.Type == Claims.SendAccessClaims.SendId).Value);

        // Verify claims
        Assert.Contains(sub.Claims, c => c.Type == Claims.SendAccessClaims.SendId && c.Value == sendId.ToString());
        Assert.Contains(sub.Claims, c => c.Type == Claims.SendAccessClaims.Email && c.Value == email);
        Assert.Contains(sub.Claims, c => c.Type == Claims.Type && c.Value == IdentityClientType.Send.ToString());

        // Verify OTP validation was called
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .Received(1)
            .ValidateTokenAsync(otp, SendAccessConstants.OtpToken.TokenProviderName, SendAccessConstants.OtpToken.Purpose, expectedUniqueId);

        // Verify no email was sent (validation only)
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendSendEmailOtpEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_InvalidOtp_ReturnsInvalidGrant(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email,
        string invalidOtp)
    {
        // Arrange
        tokenRequest.Raw = CreateValidatedTokenRequest(sendId, email, invalidOtp);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        emailOtp = emailOtp with { Emails = [email] };

        var expectedUniqueId = string.Format(SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .ValidateTokenAsync(invalidOtp,
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal("email otp is invalid.", result.ErrorDescription);

        // Verify OTP validation was attempted
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .Received(1)
            .ValidateTokenAsync(invalidOtp,
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var otpTokenProvider = Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();
        var mailService = Substitute.For<IMailService>();

        // Act
        var validator = new SendEmailOtpRequestValidator(otpTokenProvider, mailService);

        // Assert
        Assert.NotNull(validator);
    }

    private static NameValueCollection CreateValidatedTokenRequest(
        Guid sendId,
        string sendEmail = null,
        string otpCode = null)
    {
        var sendIdBase64 = CoreHelpers.Base64UrlEncode(sendId.ToByteArray());

        var rawRequestParameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, CustomGrantTypes.SendAccess },
            { OidcConstants.TokenRequest.ClientId, BitwardenClient.Send },
            { OidcConstants.TokenRequest.Scope, ApiScopes.ApiSendAccess },
            { "device_type", ((int)DeviceType.FirefoxBrowser).ToString() },
            { SendAccessConstants.TokenRequest.SendId, sendIdBase64 }
        };

        if (sendEmail != null)
        {
            rawRequestParameters.Add(SendAccessConstants.TokenRequest.Email, sendEmail);
        }

        if (otpCode != null && sendEmail != null)
        {
            rawRequestParameters.Add(SendAccessConstants.TokenRequest.Otp, otpCode);
        }

        return rawRequestParameters;
    }
}
