using Bit.Core.Tools.Models.Data;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.SendAccess;

public class SendInaccessibleRequestValidatorTests
{
    private static readonly SendInaccessible _authMethod = new();
    private readonly SendInaccessibleRequestValidator _sut = new();

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_Always_ReturnsInvalidSendId(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        Guid sendId)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId);
        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        // Act
        var result = await _sut.ValidateRequestAsync(context, _authMethod, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal(SendAccessConstants.SendIdGuidValidatorResults.InvalidSendId, result.ErrorDescription);

        var customResponse = result.CustomResponse as Dictionary<string, object>;
        Assert.NotNull(customResponse);
        Assert.Equal(
            SendAccessConstants.SendIdGuidValidatorResults.InvalidSendId,
            customResponse[SendAccessConstants.SendAccessError]);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_WithEmail_StillReturnsInvalidSendId(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        Guid sendId)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, sendEmail: "user@example.com");
        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        // Act
        var result = await _sut.ValidateRequestAsync(context, _authMethod, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal(SendAccessConstants.SendIdGuidValidatorResults.InvalidSendId, result.ErrorDescription);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_WithPassword_StillReturnsInvalidSendId(
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        Guid sendId,
        string passwordHash)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, passwordHash: passwordHash);
        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        // Act
        var result = await _sut.ValidateRequestAsync(context, _authMethod, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal(SendAccessConstants.SendIdGuidValidatorResults.InvalidSendId, result.ErrorDescription);
    }
}
