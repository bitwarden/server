using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.KeyManagement;
using Bit.Core.Models.Api;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

public interface IClientVersionValidator
{
    bool ValidateAsync(User user, CustomValidatorRequestContext requestContext);
}

/// <summary>
/// This validator will use the Client Version on a request, which currently maps
/// to the "Bitwarden-Client-Version" header, to determine if a user meets minimum
/// required client version for issuing tokens on an old client. This is done to
/// incentivize users to get on an updated client when their password encryption
/// method has already been updated.
///
/// If the header is omitted, then the validator returns that this request is valid.
/// We do this because clients can always just put whatever they want in the header,
/// and all we can do is try to prevent legitimate clients from ending up in a scenario
/// where they cannot log in due to stale encryption versions and newer client architecture.
/// </summary>
public class ClientVersionValidator(
    ICurrentContext currentContext)
    : IClientVersionValidator
{
    private const string _upgradeMessage = "Please update your app to continue using Bitwarden";
    private const string _noUserMessage = "No user found while trying to validate client version";

    public bool ValidateAsync(User? user, CustomValidatorRequestContext requestContext)
    {
        // Do this nullish check because the base request validator currently is not
        // strict null checking. Once that gets fixed then we can see about making
        // the user not nullish checked. If they are null then the validator should fail.
        if (user == null)
        {
            requestContext.ValidationErrorResult = new ValidationResult
            {
                Error = "no_user",
                ErrorDescription = _noUserMessage,
                IsError = true
            };
            requestContext.CustomResponse = new Dictionary<string, object>
            {
                { "ErrorModel", new ErrorResponseModel(_noUserMessage) }
            };
            return false;
        }

        Version? clientVersion = currentContext.ClientVersion;
        Version? minVersion = user.HasV2Encryption() ? Constants.MinimumClientVersionForV2Encryption : null;

        // Allow through if headers are missing.
        // The minVersion should never be null because of where this validator is run. The user would
        // have been determined to be null prior to reaching this point, but it is defensive programming
        // to check for nullish values in case validators were to ever be reordered.
        if (clientVersion == null || minVersion == null)
        {
            return true;
        }

        if (clientVersion < minVersion)
        {
            requestContext.ValidationErrorResult = new ValidationResult
            {
                Error = "invalid_client_version",
                ErrorDescription = _upgradeMessage,
                IsError = true
            };
            requestContext.CustomResponse = new Dictionary<string, object>
            {
                { "ErrorModel", new ErrorResponseModel(_upgradeMessage) }
            };
            return false;
        }

        return true;
    }
}


