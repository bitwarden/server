using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class AssertWebAuthnLoginCredentialCommand : IAssertWebAuthnLoginCredentialCommand
{
    private readonly IFido2 _fido2;
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWebAuthnChallengeCacheProvider _webAuthnChallengeCache;

    public AssertWebAuthnLoginCredentialCommand(
        IFido2 fido2,
        IWebAuthnCredentialRepository webAuthnCredentialRepository,
        IUserRepository userRepository,
        IWebAuthnChallengeCacheProvider webAuthnChallengeCache)
    {
        _fido2 = fido2;
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
        _userRepository = userRepository;
        _webAuthnChallengeCache = webAuthnChallengeCache;
    }

    public async Task<(User, WebAuthnCredential)> AssertWebAuthnLoginCredential(AssertionOptions options, AuthenticatorAssertionRawResponse assertionResponse)
    {
        if (!await _webAuthnChallengeCache.TryMarkChallengeAsUsedAsync(options.Challenge))
        {
            throw new BadRequestException("Invalid credential.");
        }

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
        var assertedCredentialId = assertionResponse.Id;
        var credential = userCredentials.FirstOrDefault(c => c.CredentialId == assertedCredentialId);
        if (credential == null)
        {
            throw new BadRequestException("Invalid credential.");
        }

        // Always return true, since we've already filtered the credentials after user id
        IsUserHandleOwnerOfCredentialIdAsync callback = (args, cancellationToken) => Task.FromResult(true);
        var credentialPublicKey = CoreHelpers.Base64UrlDecode(credential.PublicKey);

        VerifyAssertionResult assertionVerificationResult;
        try
        {
            assertionVerificationResult = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = options,
                StoredPublicKey = credentialPublicKey,
                StoredSignatureCounter = (uint)credential.Counter,
                IsUserHandleOwnerOfCredentialIdCallback = callback
            });
        }
        catch (Fido2VerificationException)
        {
            throw new BadRequestException("Invalid credential.");
        }

        // Update SignatureCounter
        credential.Counter = (int)assertionVerificationResult.SignCount;
        await _webAuthnCredentialRepository.ReplaceAsync(credential);

        return (user, credential);
    }
}
