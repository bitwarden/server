using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Response.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(WebAuthnController))]
[SutProviderCustomize]
public class WebAuthnControllerTests
{
    [Theory, BitAutoData]
    public async Task Get_UserNotFound_ThrowsUnauthorizedAccessException(SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsNullForAnyArgs();

        // Act
        var result = () => sutProvider.Sut.Get();

        // Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(result);
    }

    [Theory, BitAutoData]
    public async Task AttestationOptions_UserNotFound_ThrowsUnauthorizedAccessException(SecretVerificationRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsNullForAnyArgs();

        // Act
        var result = () => sutProvider.Sut.AttestationOptions(requestModel);

        // Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(result);
    }

    [Theory, BitAutoData]
    public async Task AttestationOptions_UserVerificationFailed_ThrowsBadRequestException(SecretVerificationRequestModel requestModel, User user, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, default).Returns(false);

        // Act
        var result = () => sutProvider.Sut.AttestationOptions(requestModel);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    public async Task AttestationOptions_CanUsePasskeyLoginFalse_ThrowsBadRequestException(
        SecretVerificationRequestModel requestModel, User user, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .ReturnsForAnyArgs(new RequireSsoPolicyRequirement { CanUsePasskeyLogin = false });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AttestationOptions(requestModel));
        Assert.Contains("Passkeys cannot be created for your account. SSO login is required", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AttestationOptions_CanUsePasskeyLoginTrue_Succeeds(
        SecretVerificationRequestModel requestModel, User user, SutProvider<WebAuthnController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .ReturnsForAnyArgs(new RequireSsoPolicyRequirement { CanUsePasskeyLogin = true });
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>>()
            .Protect(Arg.Any<WebAuthnCredentialCreateOptionsTokenable>()).Returns("token");

        var result = await sutProvider.Sut.AttestationOptions(requestModel);

        Assert.NotNull(result);
    }

    #region Assertion Options
    [Theory, BitAutoData]
    public async Task AssertionOptions_UserNotFound_ThrowsUnauthorizedAccessException(SecretVerificationRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsNullForAnyArgs();

        // Act
        var result = () => sutProvider.Sut.AssertionOptions(requestModel);

        // Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(result);
    }

    [Theory, BitAutoData]
    public async Task AssertionOptions_UserVerificationFailed_ThrowsBadRequestException(SecretVerificationRequestModel requestModel, User user, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, default).Returns(false);

        // Act
        var result = () => sutProvider.Sut.AssertionOptions(requestModel);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    public async Task AssertionOptions_UserVerificationSuccess_ReturnsAssertionOptions(SecretVerificationRequestModel requestModel, User user, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, requestModel.Secret).Returns(true);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Protect(Arg.Any<WebAuthnLoginAssertionOptionsTokenable>()).Returns("token");

        // Act
        var result = await sutProvider.Sut.AssertionOptions(requestModel);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<WebAuthnLoginAssertionOptionsResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task AssertionOptions_Success_ProtectsTokenWithUpdateKeySetScope(SecretVerificationRequestModel requestModel, User user, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, requestModel.Secret).Returns(true);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Protect(Arg.Any<WebAuthnLoginAssertionOptionsTokenable>()).Returns("token");

        // Act
        await sutProvider.Sut.AssertionOptions(requestModel);

        // Assert
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Received(1)
            .Protect(Arg.Is<WebAuthnLoginAssertionOptionsTokenable>(t =>
                t.Scope == Core.Auth.Enums.WebAuthnLoginAssertionOptionsScope.UpdateKeySet));
    }
    #endregion

    [Theory, BitAutoData]
    public async Task Post_UserNotFound_ThrowsUnauthorizedAccessException(WebAuthnLoginCredentialCreateRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsNullForAnyArgs();

        // Act
        var result = () => sutProvider.Sut.Post(requestModel);

        // Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(result);
    }

    [Theory, BitAutoData]
    public async Task Post_ExpiredToken_ThrowsBadRequestException(WebAuthnLoginCredentialCreateRequestModel requestModel, CredentialCreateOptions createOptions, User user, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, createOptions);
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .ReturnsForAnyArgs(new RequireSsoPolicyRequirement { CanUsePasskeyLogin = true });

        // Act
        var result = () => sutProvider.Sut.Post(requestModel);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    public async Task Post_ValidInput_ReturnsCredential(WebAuthnLoginCredentialCreateRequestModel requestModel, CredentialCreateOptions createOptions, User user, WebAuthnCredential credential, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, createOptions);
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);
        sutProvider.GetDependency<ICreateWebAuthnLoginCredentialCommand>()
            .CreateWebAuthnLoginCredentialAsync(user, requestModel.Name, createOptions, Arg.Any<AuthenticatorAttestationRawResponse>(), requestModel.SupportsPrf, requestModel.EncryptedUserKey, requestModel.EncryptedPublicKey, requestModel.EncryptedPrivateKey)
            .Returns(credential);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .ReturnsForAnyArgs(new RequireSsoPolicyRequirement { CanUsePasskeyLogin = true });

        // Act
        var result = await sutProvider.Sut.Post(requestModel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(credential.Id.ToString(), result.Id);
        await sutProvider.GetDependency<ICreateWebAuthnLoginCredentialCommand>()
            .Received(1)
            .CreateWebAuthnLoginCredentialAsync(user, requestModel.Name, createOptions, Arg.Any<AuthenticatorAttestationRawResponse>(), requestModel.SupportsPrf, requestModel.EncryptedUserKey, requestModel.EncryptedPublicKey, requestModel.EncryptedPrivateKey);
    }

    [Theory, BitAutoData]
    public async Task Post_CredentialCreationFailed_ThrowsBadRequestException(WebAuthnLoginCredentialCreateRequestModel requestModel, CredentialCreateOptions createOptions, User user, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, createOptions);
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);
        sutProvider.GetDependency<ICreateWebAuthnLoginCredentialCommand>()
            .CreateWebAuthnLoginCredentialAsync(user, requestModel.Name, createOptions, Arg.Any<AuthenticatorAttestationRawResponse>(), requestModel.SupportsPrf, requestModel.EncryptedUserKey, requestModel.EncryptedPublicKey, requestModel.EncryptedPrivateKey)
            .Returns((WebAuthnCredential)null);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .ReturnsForAnyArgs(new RequireSsoPolicyRequirement { CanUsePasskeyLogin = true });

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Post(requestModel));
    }

    [Theory, BitAutoData]
    public async Task Post_CanUsePasskeyLoginFalse_ThrowsBadRequestException(
        WebAuthnLoginCredentialCreateRequestModel requestModel,
        CredentialCreateOptions createOptions,
        User user,
        SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, createOptions);
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .ReturnsForAnyArgs(new RequireSsoPolicyRequirement { CanUsePasskeyLogin = false });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.Post(requestModel));
        Assert.Contains("Passkeys cannot be created for your account. SSO login is required", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task Post_CanUsePasskeyLoginTrue_Succeeds(
        WebAuthnLoginCredentialCreateRequestModel requestModel,
        CredentialCreateOptions createOptions,
        User user,
        WebAuthnCredential credential,
        SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var token = new WebAuthnCredentialCreateOptionsTokenable(user, createOptions);
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);
        sutProvider.GetDependency<ICreateWebAuthnLoginCredentialCommand>()
            .CreateWebAuthnLoginCredentialAsync(user, requestModel.Name, createOptions, Arg.Any<AuthenticatorAttestationRawResponse>(), requestModel.SupportsPrf, requestModel.EncryptedUserKey, requestModel.EncryptedPublicKey, requestModel.EncryptedPrivateKey)
            .Returns(credential);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnCredentialCreateOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsyncVNext<RequireSsoPolicyRequirement>(user.Id)
            .ReturnsForAnyArgs(new RequireSsoPolicyRequirement { CanUsePasskeyLogin = true });

        // Act
        await sutProvider.Sut.Post(requestModel);

        // Assert
        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .GetUserByPrincipalAsync(default);
        await sutProvider.GetDependency<ICreateWebAuthnLoginCredentialCommand>()
            .Received(1)
            .CreateWebAuthnLoginCredentialAsync(user, requestModel.Name, createOptions, Arg.Any<AuthenticatorAttestationRawResponse>(), requestModel.SupportsPrf, requestModel.EncryptedUserKey, requestModel.EncryptedPublicKey, requestModel.EncryptedPrivateKey);
    }

    [Theory, BitAutoData]
    public async Task Delete_UserNotFound_ThrowsUnauthorizedAccessException(Guid credentialId, SecretVerificationRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsNullForAnyArgs();

        // Act
        var result = () => sutProvider.Sut.Delete(credentialId, requestModel);

        // Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(result);
    }

    [Theory, BitAutoData]
    public async Task Delete_UserVerificationFailed_ThrowsBadRequestException(Guid credentialId, SecretVerificationRequestModel requestModel, User user, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, default).Returns(false);

        // Act
        var result = () => sutProvider.Sut.Delete(credentialId, requestModel);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    #region Update Credential
    [Theory, BitAutoData]
    public async Task Put_TokenVerificationFailed_ThrowsBadRequestException(AssertionOptions assertionOptions, WebAuthnLoginCredentialUpdateRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var expectedMessage = "The token associated with your request is invalid or has expired. A valid token is required to continue.";
        var token = new WebAuthnLoginAssertionOptionsTokenable(
            Core.Auth.Enums.WebAuthnLoginAssertionOptionsScope.PrfRegistration, assertionOptions);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateCredential(requestModel));
        // Assert
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task Put_TokenWithNullOptions_ThrowsBadRequestException(WebAuthnLoginCredentialUpdateRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange - tokenable deserialized with correct scope but Options == null
        var expectedMessage = "The token associated with your request is invalid or has expired. A valid token is required to continue.";
        var token = new WebAuthnLoginAssertionOptionsTokenable
        {
            Scope = Core.Auth.Enums.WebAuthnLoginAssertionOptionsScope.UpdateKeySet,
            Options = null,
        };
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateCredential(requestModel));

        // Assert
        Assert.Equal(expectedMessage, exception.Message);
        await sutProvider.GetDependency<IAssertWebAuthnLoginCredentialCommand>()
            .DidNotReceive()
            .AssertWebAuthnLoginCredential(Arg.Any<AssertionOptions>(), Arg.Any<AuthenticatorAssertionRawResponse>());
    }

    [Theory, BitAutoData]
    public async Task Put_CredentialNotFound_ThrowsBadRequestException(AssertionOptions assertionOptions, WebAuthnLoginCredentialUpdateRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var expectedMessage = "Unable to update credential.";
        var token = new WebAuthnLoginAssertionOptionsTokenable(
            Core.Auth.Enums.WebAuthnLoginAssertionOptionsScope.UpdateKeySet, assertionOptions);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateCredential(requestModel));
        // Assert
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task Put_PrfNotSupported_ThrowsBadRequestException(User user, WebAuthnCredential credential, AssertionOptions assertionOptions, WebAuthnLoginCredentialUpdateRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var expectedMessage = "Unable to update credential.";
        credential.SupportsPrf = false;
        var token = new WebAuthnLoginAssertionOptionsTokenable(
            Core.Auth.Enums.WebAuthnLoginAssertionOptionsScope.UpdateKeySet, assertionOptions);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);

        sutProvider.GetDependency<IAssertWebAuthnLoginCredentialCommand>()
            .AssertWebAuthnLoginCredential(assertionOptions, requestModel.DeviceResponse)
            .Returns((user, credential));

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateCredential(requestModel));
        // Assert
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task Put_UpdateCredential_Success(User user, WebAuthnCredential credential, AssertionOptions assertionOptions, WebAuthnLoginCredentialUpdateRequestModel requestModel, SutProvider<WebAuthnController> sutProvider)
    {
        // Arrange
        var token = new WebAuthnLoginAssertionOptionsTokenable(
            Core.Auth.Enums.WebAuthnLoginAssertionOptionsScope.UpdateKeySet, assertionOptions);
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Unprotect(requestModel.Token)
            .Returns(token);

        sutProvider.GetDependency<IAssertWebAuthnLoginCredentialCommand>()
            .AssertWebAuthnLoginCredential(assertionOptions, requestModel.DeviceResponse)
            .Returns((user, credential));

        // Act
        await sutProvider.Sut.UpdateCredential(requestModel);

        // Assert
        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .Received(1)
            .Unprotect(requestModel.Token);
        await sutProvider.GetDependency<IAssertWebAuthnLoginCredentialCommand>()
            .Received(1)
            .AssertWebAuthnLoginCredential(assertionOptions, requestModel.DeviceResponse);
        await sutProvider.GetDependency<IWebAuthnCredentialRepository>()
            .Received(1)
            .UpdateAsync(credential);
    }
    #endregion
}
