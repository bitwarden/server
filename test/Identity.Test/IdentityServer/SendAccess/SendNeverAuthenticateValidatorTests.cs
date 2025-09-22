using Bit.Core.Tools.Models.Data;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.SendAccess;

[SutProviderCustomize]
public class SendNeverAuthenticateRequestValidatorTests
{
    /// <summary>
    /// To support the static hashing function <see cref="EnumerationProtectionHelpers.GetIndexForSaltHash"/> theses GUIDs and Key must be hardcoded
    /// </summary>
    private static readonly string _testHashKey = "test-key-123456789012345678901234567890";
    // These Guids are static and ensure the correct index for each error type
    private static readonly Guid _invalidSendGuid = Guid.Parse("1b35fbf3-a14a-4d48-82b7-2adc34fdae6f");
    private static readonly Guid _emailSendGuid = Guid.Parse("bc8e2ef5-a0bd-44d2-bdb7-5902be6f5c41");
    private static readonly Guid _passwordSendGuid = Guid.Parse("da36fa27-f0e8-4701-a585-d3d8c2f67c4b");

    private static readonly NeverAuthenticate _authMethod = new();

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_GuidErrorSelected_ReturnsInvalidSendId(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(_invalidSendGuid);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = _testHashKey;

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, _invalidSendGuid);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal(SendAccessConstants.SendIdGuidValidationResults.InvalidSendId, result.ErrorDescription);

        var customResponse = result.CustomResponse as Dictionary<string, object>;
        Assert.NotNull(customResponse);
        Assert.Equal(
            SendAccessConstants.SendIdGuidValidationResults.InvalidSendId, customResponse[SendAccessConstants.SendAccessError]);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_EmailErrorSelected_HasEmail_ReturnsEmailInvalid(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        string email)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(_emailSendGuid, sendEmail: email);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = _testHashKey;

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, _emailSendGuid);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal(SendAccessConstants.EmailOtpValidatorResults.EmailInvalid, result.ErrorDescription);

        var customResponse = result.CustomResponse as Dictionary<string, object>;
        Assert.NotNull(customResponse);
        Assert.Equal(SendAccessConstants.EmailOtpValidatorResults.EmailInvalid, customResponse[SendAccessConstants.SendAccessError]);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_EmailErrorSelected_NoEmail_ReturnsEmailRequired(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(_emailSendGuid);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = _testHashKey;

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, _emailSendGuid);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal(SendAccessConstants.EmailOtpValidatorResults.EmailRequired, result.ErrorDescription);

        var customResponse = result.CustomResponse as Dictionary<string, object>;
        Assert.NotNull(customResponse);
        Assert.Equal(SendAccessConstants.EmailOtpValidatorResults.EmailRequired, customResponse[SendAccessConstants.SendAccessError]);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_PasswordErrorSelected_HasPassword_ReturnsPasswordDoesNotMatch(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        string password)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(_passwordSendGuid, passwordHash: password);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = _testHashKey;

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, _passwordSendGuid);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal(SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch, result.ErrorDescription);

        var customResponse = result.CustomResponse as Dictionary<string, object>;
        Assert.NotNull(customResponse);
        Assert.Equal(SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch, customResponse[SendAccessConstants.SendAccessError]);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_PasswordErrorSelected_NoPassword_ReturnsPasswordRequired(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest)
    {
        // Arrange

        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(_passwordSendGuid);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };
        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = _testHashKey;

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, _passwordSendGuid);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal(SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired, result.ErrorDescription);

        var customResponse = result.CustomResponse as Dictionary<string, object>;
        Assert.NotNull(customResponse);
        Assert.Equal(SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired, customResponse[SendAccessConstants.SendAccessError]);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_NullHashKey_UsesEmptyKey(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(_invalidSendGuid);
        var context = new ExtensionGrantValidationContext { Request = tokenRequest };
        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = null;

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, _invalidSendGuid);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Contains(result.ErrorDescription, SendAccessConstants.SendIdGuidValidationResults.InvalidSendId);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_EmptyHashKey_UsesEmptyKey(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(_invalidSendGuid);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = "";

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, _invalidSendGuid);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Contains(result.ErrorDescription, SendAccessConstants.SendIdGuidValidationResults.InvalidSendId);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_ConsistentBehavior_SameSendIdSameResult(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        Guid sendId)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = "consistent-test-key-123456789012345678901234567890";

        // Act
        var result1 = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, sendId);
        var result2 = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, sendId);

        // Assert
        Assert.Equal(result1.ErrorDescription, result2.ErrorDescription);
        Assert.Equal(result1.Error, result2.Error);

        var customResponse1 = result1.CustomResponse as Dictionary<string, object>;
        var customResponse2 = result2.CustomResponse as Dictionary<string, object>;
        Assert.Equal(customResponse1[SendAccessConstants.SendAccessError], customResponse2[SendAccessConstants.SendAccessError]);
    }

    [Theory, BitAutoData]
    public async Task ValidateRequestAsync_DifferentSendIds_CanReturnDifferentResults(
        SutProvider<SendNeverAuthenticateRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        Guid sendId1,
        Guid sendId2)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId1);
        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<Core.Settings.GlobalSettings>().SendDefaultHashKey = "different-test-key-123456789012345678901234567890";

        // Act
        var result1 = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, sendId1);
        var result2 = await sutProvider.Sut.ValidateRequestAsync(context, _authMethod, sendId2);

        // Assert - Both should be errors
        Assert.True(result1.IsError);
        Assert.True(result2.IsError);

        // Both should have valid error types
        var validErrors = new[]
        {
            SendAccessConstants.SendIdGuidValidationResults.InvalidSendId,
            SendAccessConstants.EmailOtpValidatorResults.EmailRequired,
            SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired
        };
        Assert.Contains(result1.ErrorDescription, validErrors);
        Assert.Contains(result2.ErrorDescription, validErrors);
    }

    [Fact]
    public void Constructor_WithValidGlobalSettings_CreatesInstance()
    {
        // Arrange
        var globalSettings = new Core.Settings.GlobalSettings
        {
            SendDefaultHashKey = "test-key-123456789012345678901234567890"
        };

        // Act
        var validator = new SendNeverAuthenticateRequestValidator(globalSettings);

        // Assert
        Assert.NotNull(validator);
    }
}
