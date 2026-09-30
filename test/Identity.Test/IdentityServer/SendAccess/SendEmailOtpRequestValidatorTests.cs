using System.Globalization;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
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
    private const string DeviceIdentifier = "device-identifier";

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_MissingEmail_ReturnsInvalidRequest(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        ArrangeDeviceIdentifier(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal("email is required.", result.ErrorDescription);

        // Verify no OTP generation or email sending occurred
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceive()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

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
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        ArrangeDeviceIdentifier(sutProvider);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal("email and otp are required.", result.ErrorDescription);

        // Verify no OTP generation or email sending occurred
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceive()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

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
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        ArrangeDeviceIdentifier(sutProvider);

        var expectedUniqueId = string.Format(CultureInfo.InvariantCulture, SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .GenerateTokenAsync(
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId,
                DeviceIdentifier)
            .Returns(generatedToken);

        emailOtp = emailOtp with { emails = [email] };

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal("email and otp are required.", result.ErrorDescription);

        // Verify OTP generation
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .Received(1)
            .GenerateTokenAsync(
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId,
                DeviceIdentifier);

        // Verify email sending
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendSendEmailOtpEmailAsync(email, generatedToken, Arg.Any<string>());
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
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        ArrangeDeviceIdentifier(sutProvider);

        emailOtp = emailOtp with { emails = [email] };

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
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
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email, otp);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        ArrangeDeviceIdentifier(sutProvider);

        emailOtp = emailOtp with { emails = [email] };

        var expectedUniqueId = string.Format(CultureInfo.InvariantCulture, SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .ValidateTokenAsync(
                otp,
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId,
                DeviceIdentifier)
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
            .ValidateTokenAsync(otp, SendAccessConstants.OtpToken.TokenProviderName, SendAccessConstants.OtpToken.Purpose, expectedUniqueId, DeviceIdentifier);

        // Verify no email was sent (validation only)
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendSendEmailOtpEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_MixedCaseEmail_NormalizesEmailForCacheKeyAndClaim(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string otp)
    {
        // Arrange
        const string mixedCaseEmail = " Alice@Example.COM ";
        const string normalizedEmail = "alice@example.com";

        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, mixedCaseEmail, otp);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        ArrangeDeviceIdentifier(sutProvider);

        // The allow-list keeps the original casing; the case-insensitive membership check must still
        // match after the request email is normalized.
        emailOtp = emailOtp with { emails = [mixedCaseEmail.Trim()] };

        // The OTP cache key is built from the normalized email, so the stub must key off it.
        var expectedUniqueId = string.Format(CultureInfo.InvariantCulture, SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, normalizedEmail);

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .ValidateTokenAsync(
                otp,
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId,
                DeviceIdentifier)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.False(result.IsError);

        // The send_access email claim carries the normalized address so event-log accessor attribution
        // resolves the same User regardless of the database provider's collation case sensitivity.
        Assert.Contains(result.Subject.Claims, c => c.Type == Claims.SendAccessClaims.Email && c.Value == normalizedEmail);

        // OTP validation ran against the normalized cache key, not the raw mixed-case input.
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .Received(1)
            .ValidateTokenAsync(otp, SendAccessConstants.OtpToken.TokenProviderName, SendAccessConstants.OtpToken.Purpose, expectedUniqueId, DeviceIdentifier);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_InvalidOtp_ReturnsInvalidRequest(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email,
        string invalidOtp)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email, invalidOtp);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        ArrangeDeviceIdentifier(sutProvider);

        emailOtp = emailOtp with { emails = [email] };

        var expectedUniqueId = string.Format(CultureInfo.InvariantCulture, SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .ValidateTokenAsync(invalidOtp,
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId,
                DeviceIdentifier)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal($"{SendAccessConstants.TokenRequest.Email} and {SendAccessConstants.TokenRequest.Otp} are required.", result.ErrorDescription);

        // Verify OTP validation was attempted
        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .Received(1)
            .ValidateTokenAsync(invalidOtp,
                SendAccessConstants.OtpToken.TokenProviderName,
                SendAccessConstants.OtpToken.Purpose,
                expectedUniqueId,
                DeviceIdentifier);
    }

    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task ValidateRequestAsync_MissingDeviceIdentifier_ReturnsDeviceIdentifierRequiredWithoutSendingOtp(
        string deviceIdentifier,
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(deviceIdentifier);

        emailOtp = emailOtp with { emails = [email] };

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal("Device-Identifier header is required.", result.ErrorDescription);
        Assert.Equal(
            SendAccessConstants.EmailOtpValidatorResults.DeviceIdentifierRequired,
            result.CustomResponse[SendAccessConstants.SendAccessError]);

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceive()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendSendEmailOtpEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_MissingDeviceIdentifier_WithOtp_DoesNotValidateOtp(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email,
        string otp)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email, otp);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns((string)null);

        emailOtp = emailOtp with { emails = [email] };

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal(
            SendAccessConstants.EmailOtpValidatorResults.DeviceIdentifierRequired,
            result.CustomResponse[SendAccessConstants.SendAccessError]);

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceive()
            .ValidateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_DeviceIdentifierTooLong_ReturnsDeviceIdentifierInvalidWithoutSendingOtp(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier
            .Returns(new string('a', Device.MaxIdentifierLength + 1));

        emailOtp = emailOtp with { emails = [email] };

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal("Device-Identifier header is invalid.", result.ErrorDescription);
        Assert.Equal(
            SendAccessConstants.EmailOtpValidatorResults.DeviceIdentifierInvalid,
            result.CustomResponse[SendAccessConstants.SendAccessError]);

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .DidNotReceive()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendSendEmailOtpEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_DeviceIdentifierAtMaxLength_GeneratesOtpBoundToIt(
        SutProvider<SendEmailOtpRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        EmailOtp emailOtp,
        Guid sendId,
        string email,
        string generatedToken)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        var maxLengthDeviceIdentifier = new string('a', Device.MaxIdentifierLength);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(maxLengthDeviceIdentifier);

        emailOtp = emailOtp with { emails = [email] };

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .GenerateTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), maxLengthDeviceIdentifier)
            .Returns(generatedToken);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, emailOtp, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("email and otp are required.", result.ErrorDescription);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendSendEmailOtpEmailAsync(email, generatedToken, Arg.Any<string>());
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var otpTokenProvider = Substitute.For<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>();
        var mailService = Substitute.For<IMailService>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<SendEmailOtpRequestValidator>>();
        var currentContext = Substitute.For<ICurrentContext>();
        // Act
        var validator = new SendEmailOtpRequestValidator(logger, otpTokenProvider, mailService, currentContext);

        // Assert
        Assert.NotNull(validator);
    }

    private static void ArrangeDeviceIdentifier(SutProvider<SendEmailOtpRequestValidator> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(DeviceIdentifier);
    }
}
