﻿using System.Collections.Specialized;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.SendAccess;

[SutProviderCustomize]
public class SendAccessGrantValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_FeatureFlagDisabled_ReturnsUnsupportedGrantType(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendAccess)
            .Returns(false);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.True(context.Result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.UnsupportedGrantType, context.Result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MissingSendId_ReturnsInvalidRequest(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendAccess)
            .Returns(true);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, context.Result.Error);
        Assert.Equal($"{SendAccessConstants.TokenRequest.SendId} is required.", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_InvalidSendId_ReturnsInvalidGrant(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendAccess)
            .Returns(true);

        var context = new ExtensionGrantValidationContext();

        tokenRequest.GrantType = CustomGrantTypes.SendAccess;
        tokenRequest.Raw = CreateTokenRequestBody(Guid.Empty);

        // To preserve the CreateTokenRequestBody method for more general usage we over write the sendId
        tokenRequest.Raw.Set(SendAccessConstants.TokenRequest.SendId, "invalid-guid-format");
        context.Request = tokenRequest;

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, context.Result.Error);
        Assert.Equal($"{SendAccessConstants.TokenRequest.SendId} is invalid.", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EmptyGuidSendId_ReturnsInvalidGrant(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider)
    {
        // Arrange
        var context = SetupTokenRequest(
            sutProvider,
            Guid.Empty, // Empty Guid as sendId
            tokenRequest);

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, context.Result.Error);
        Assert.Equal($"{SendAccessConstants.TokenRequest.SendId} is invalid.", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NeverAuthenticateMethod_ReturnsInvalidGrant(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider,
        Guid sendId)
    {
        // Arrange
        var context = SetupTokenRequest(
            sutProvider,
            sendId,
            tokenRequest);

        sutProvider.GetDependency<ISendAuthenticationQuery>()
            .GetAuthenticationMethod(sendId)
            .Returns(new NeverAuthenticate());

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, context.Result.Error);
        Assert.Equal($"{SendAccessConstants.TokenRequest.SendId} is invalid.", context.Result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NotAuthenticatedMethod_ReturnsSuccess(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider,
        Guid sendId)
    {
        // Arrange
        var context = SetupTokenRequest(
            sutProvider,
            sendId,
            tokenRequest);

        sutProvider.GetDependency<ISendAuthenticationQuery>()
            .GetAuthenticationMethod(sendId)
            .Returns(new NotAuthenticated());

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.False(context.Result.IsError);
        // get the claims principal from the result
        var subject = context.Result.Subject;
        Assert.NotNull(subject);
        Assert.Equal(sendId.ToString(), subject.GetSubjectId());
        Assert.Equal(CustomGrantTypes.SendAccess, subject.GetAuthenticationMethod());
        // get the claims from the subject
        var claims = subject.Claims.ToList();
        Assert.NotEmpty(claims);
        Assert.Contains(claims, c => c.Type == Claims.SendAccessClaims.SendId && c.Value == sendId.ToString());
        Assert.Contains(claims, c => c.Type == Claims.Type && c.Value == IdentityClientType.Send.ToString());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ResourcePasswordMethod_CallsPasswordValidator(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider,
        Guid sendId,
        ResourcePassword resourcePassword,
        GrantValidationResult expectedResult)
    {
        // Arrange
        var context = SetupTokenRequest(
            sutProvider,
            sendId,
            tokenRequest);

        sutProvider.GetDependency<ISendAuthenticationQuery>()
            .GetAuthenticationMethod(sendId)
            .Returns(resourcePassword);

        sutProvider.GetDependency<ISendAuthenticationMethodValidator<ResourcePassword>>()
            .ValidateRequestAsync(context, resourcePassword, sendId)
            .Returns(expectedResult);

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal(expectedResult, context.Result);
        await sutProvider.GetDependency<ISendAuthenticationMethodValidator<ResourcePassword>>()
            .Received(1)
            .ValidateRequestAsync(context, resourcePassword, sendId);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EmailOtpMethod_CallsEmailOtp(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider,
        GrantValidationResult expectedResult,
        Guid sendId,
        EmailOtp emailOtp)
    {
        // Arrange
        var context = SetupTokenRequest(
            sutProvider,
            sendId,
            tokenRequest);

        sutProvider.GetDependency<ISendAuthenticationQuery>()
            .GetAuthenticationMethod(sendId)
            .Returns(emailOtp);

        sutProvider.GetDependency<ISendAuthenticationMethodValidator<EmailOtp>>()
            .ValidateRequestAsync(context, emailOtp, sendId)
            .Returns(expectedResult);

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal(expectedResult, context.Result);
        await sutProvider.GetDependency<ISendAuthenticationMethodValidator<EmailOtp>>()
            .Received(1)
            .ValidateRequestAsync(context, emailOtp, sendId);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UnknownAuthMethod_ThrowsInvalidOperationException(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<SendAccessGrantValidator> sutProvider,
        Guid sendId)
    {
        // Arrange
        var context = SetupTokenRequest(
            sutProvider,
            sendId,
            tokenRequest);

        // Create a mock authentication method that's not handled
        var unknownMethod = Substitute.For<SendAuthenticationMethod>();
        sutProvider.GetDependency<ISendAuthenticationQuery>()
            .GetAuthenticationMethod(sendId)
            .Returns(unknownMethod);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.ValidateAsync(context));

        Assert.StartsWith("Unknown auth method:", exception.Message);
    }

    [Fact]
    public void GrantType_ReturnsCorrectType()
    {
        // Arrange & Act
        var validator = new SendAccessGrantValidator(null!, null!, null!, null!);

        // Assert
        Assert.Equal(CustomGrantTypes.SendAccess, ((IExtensionGrantValidator)validator).GrantType);
    }

    /// <summary>
    /// Mutator method fo the SutProvider and the Context to set up a valid request
    /// </summary>
    /// <param name="sutProvider">current sut provider</param>
    /// <param name="context">test context</param>
    /// <param name="sendId">the send id</param>
    /// <param name="request">the token request</param>
    private static ExtensionGrantValidationContext SetupTokenRequest(
        SutProvider<SendAccessGrantValidator> sutProvider,
        Guid sendId,
        ValidatedTokenRequest request)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SendAccess)
            .Returns(true);

        var context = new ExtensionGrantValidationContext();

        request.GrantType = CustomGrantTypes.SendAccess;
        request.Raw = CreateTokenRequestBody(sendId);
        context.Request = request;

        return context;
    }

    private static NameValueCollection CreateTokenRequestBody(
        Guid sendId,
        string passwordHash = null,
        string sendEmail = null,
        string otpCode = null)
    {
        var sendIdBase64 = CoreHelpers.Base64UrlEncode(sendId.ToByteArray());

        var rawRequestParameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, CustomGrantTypes.SendAccess },
            { OidcConstants.TokenRequest.ClientId, BitwardenClient.Send },
            { OidcConstants.TokenRequest.Scope, ApiScopes.ApiSendAccess },
            { "deviceType", ((int)DeviceType.FirefoxBrowser).ToString() },
            { SendAccessConstants.TokenRequest.SendId, sendIdBase64 }
        };

        if (passwordHash != null)
        {
            rawRequestParameters.Add(SendAccessConstants.TokenRequest.ClientB64HashedPassword, passwordHash);
        }

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
