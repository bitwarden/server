using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Fido2NetLib;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class AssertWebAuthnLoginCredentialCommand : IAssertWebAuthnLoginCredentialCommand
{
    private readonly IFido2 _fido2;
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;
    private readonly IUserRepository _userRepository;

    public AssertWebAuthnLoginCredentialCommand(IFido2 fido2, IWebAuthnCredentialRepository webAuthnCredentialRepository, IUserRepository userRepository)
    {
        _fido2 = fido2;
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
        _userRepository = userRepository;
    }

    public async Task<(User, WebAuthnCredential)> AssertWebAuthnLoginCredential(AssertionOptions options, AuthenticatorAssertionRawResponse assertionResponse)
    {
        if (!GuidUtilities.TryParseBytes(assertionResponse.Response.UserHandle, out var userId))
        {
            throw new BadRequestException("Invalid credential.");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new BadRequestException("Invalid credential.");
        }

        var userCredentials = await _webAuthnCredentialRepository.GetManyByUserIdAsync(user.Id);
        var assertedCredentialId = CoreHelpers.Base64UrlEncode(assertionResponse.Id);
        var credential = userCredentials.FirstOrDefault(c => c.CredentialId == assertedCredentialId);
        if (credential == null)
        {
            throw new BadRequestException("Invalid credential.");
        }

        // Always return true, since we've already filtered the credentials after user id
        IsUserHandleOwnerOfCredentialIdAsync callback = (args, cancellationToken) => Task.FromResult(true);
        var credentialPublicKey = CoreHelpers.Base64UrlDecode(credential.PublicKey);
        var assertionVerificationResult = await _fido2.MakeAssertionAsync(
            assertionResponse, options, credentialPublicKey, (uint)credential.Counter, callback);

        // Update SignatureCounter
        credential.Counter = (int)assertionVerificationResult.Counter;
        await _webAuthnCredentialRepository.ReplaceAsync(credential);

        if (assertionVerificationResult.Status != "ok")
        {
            throw new BadRequestException("Invalid credential.");
        }

        return (user, credential);
    }
}
