using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin;

public record CredentialLimitReached() : BadRequestError(
    $"You have reached the maximum number of passkeys ({CreateWebAuthnLoginCredentialCommand.MaxCredentialsPerUser}).");
