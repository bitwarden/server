using Bit.Core.Auth.Identity;
using Bit.Core.Auth.UserFeatures.SendAccess;
using Bit.Core.KeyManagement.Sends;
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
public class SendPasswordRequestValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateSendPassword_MissingPasswordHash_ReturnsInvalidRequest(
        SutProvider<SendPasswordRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        ResourcePassword resourcePassword,
        Guid sendId)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, resourcePassword, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidRequest, result.Error);
        Assert.Equal($"{SendAccessConstants.TokenRequest.ClientB64HashedPassword} is required.", result.ErrorDescription);

        // Verify password hasher was not called
        sutProvider.GetDependency<ISendPasswordHasher>()
            .DidNotReceive()
            .PasswordHashMatches(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateSendPassword_PasswordHashMismatch_ReturnsInvalidGrant(
        SutProvider<SendPasswordRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        ResourcePassword resourcePassword,
        Guid sendId,
        string clientPasswordHash)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, passwordHash: clientPasswordHash);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<ISendPasswordHasher>()
            .PasswordHashMatches(resourcePassword.Hash, clientPasswordHash)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, resourcePassword, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);
        Assert.Equal($"{SendAccessConstants.TokenRequest.ClientB64HashedPassword} is invalid.", result.ErrorDescription);

        // Verify password hasher was called with correct parameters
        sutProvider.GetDependency<ISendPasswordHasher>()
            .Received(1)
            .PasswordHashMatches(resourcePassword.Hash, clientPasswordHash);
    }

    [Theory, BitAutoData]
    public async Task ValidateSendPassword_PasswordHashMatches_ReturnsSuccess(
        SutProvider<SendPasswordRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        ResourcePassword resourcePassword,
        Guid sendId,
        string clientPasswordHash)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, passwordHash: clientPasswordHash);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<ISendPasswordHasher>()
            .PasswordHashMatches(resourcePassword.Hash, clientPasswordHash)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, resourcePassword, sendId);

        // Assert
        Assert.False(result.IsError);

        var sub = result.Subject;
        Assert.Equal(sendId, sub.GetSendId());

        // Verify claims
        Assert.Contains(sub.Claims, c => c.Type == Claims.SendAccessClaims.SendId && c.Value == sendId.ToString());
        Assert.Contains(sub.Claims, c => c.Type == Claims.Type && c.Value == IdentityClientType.Send.ToString());

        // Verify password hasher was called
        sutProvider.GetDependency<ISendPasswordHasher>()
            .Received(1)
            .PasswordHashMatches(resourcePassword.Hash, clientPasswordHash);
    }

    [Theory, BitAutoData]
    public async Task ValidateSendPassword_EmptyPasswordHash_CallsPasswordHasher(
        SutProvider<SendPasswordRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        ResourcePassword resourcePassword,
        Guid sendId)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, passwordHash: string.Empty);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<ISendPasswordHasher>()
            .PasswordHashMatches(resourcePassword.Hash, string.Empty)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, resourcePassword, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);

        // Verify password hasher was called with empty string
        sutProvider.GetDependency<ISendPasswordHasher>()
            .Received(1)
            .PasswordHashMatches(resourcePassword.Hash, string.Empty);
    }

    [Theory, BitAutoData]
    public async Task ValidateSendPassword_WhitespacePasswordHash_CallsPasswordHasher(
        SutProvider<SendPasswordRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        ResourcePassword resourcePassword,
        Guid sendId)
    {
        // Arrange
        var whitespacePassword = "   ";
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, passwordHash: whitespacePassword);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<ISendPasswordHasher>()
            .PasswordHashMatches(resourcePassword.Hash, whitespacePassword)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, resourcePassword, sendId);

        // Assert
        Assert.True(result.IsError);

        // Verify password hasher was called with whitespace string
        sutProvider.GetDependency<ISendPasswordHasher>()
            .Received(1)
            .PasswordHashMatches(resourcePassword.Hash, whitespacePassword);
    }

    [Theory, BitAutoData]
    public async Task ValidateSendPassword_MultiplePasswordHashParameters_ReturnsInvalidGrant(
        SutProvider<SendPasswordRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        ResourcePassword resourcePassword,
        Guid sendId)
    {
        // Arrange
        var firstPassword = "first-password";
        var secondPassword = "second-password";
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, passwordHash: [firstPassword, secondPassword]);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<ISendPasswordHasher>()
            .PasswordHashMatches(resourcePassword.Hash, firstPassword)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, resourcePassword, sendId);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(OidcConstants.TokenErrors.InvalidGrant, result.Error);

        // Verify password hasher was called with first value
        sutProvider.GetDependency<ISendPasswordHasher>()
            .Received(1)
            .PasswordHashMatches(resourcePassword.Hash, $"{firstPassword},{secondPassword}");
    }

    [Theory, BitAutoData]
    public async Task ValidateSendPassword_SuccessResult_ContainsCorrectClaims(
        SutProvider<SendPasswordRequestValidator> sutProvider,
        [AutoFixture.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        ResourcePassword resourcePassword,
        Guid sendId,
        string clientPasswordHash)
    {
        // Arrange
        tokenRequest.Raw = SendAccessTestUtilities.CreateValidatedTokenRequest(sendId, passwordHash: clientPasswordHash);

        var context = new ExtensionGrantValidationContext
        {
            Request = tokenRequest
        };

        sutProvider.GetDependency<ISendPasswordHasher>()
            .PasswordHashMatches(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateRequestAsync(context, resourcePassword, sendId);

        // Assert
        Assert.False(result.IsError);
        var sub = result.Subject;

        var sendIdClaim = sub.Claims.FirstOrDefault(c => c.Type == Claims.SendAccessClaims.SendId);
        Assert.NotNull(sendIdClaim);
        Assert.Equal(sendId.ToString(), sendIdClaim.Value);

        var typeClaim = sub.Claims.FirstOrDefault(c => c.Type == Claims.Type);
        Assert.NotNull(typeClaim);
        Assert.Equal(IdentityClientType.Send.ToString(), typeClaim.Value);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var sendPasswordHasher = Substitute.For<ISendPasswordHasher>();

        // Act
        var validator = new SendPasswordRequestValidator(sendPasswordHasher);

        // Assert
        Assert.NotNull(validator);
    }
}
