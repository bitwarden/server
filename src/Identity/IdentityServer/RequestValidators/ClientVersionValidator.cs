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
/// If the header is omitted, then the validator returns that this request is invalid.
/// </summary>
public class ClientVersionValidator(
    ICurrentContext currentContext)
    : IClientVersionValidator
{
    private const string _upgradeMessage = "Please update your app to continue using Bitwarden";
    private const string _noUserMessage = "No user found while trying to validate client version";
    private const string _versionHeaderMissing = "No client version header found, required to prevent encryption errors. Please confirm your client is supplying the header: \"Bitwarden-Client-Version\"";

    public bool ValidateAsync(User? user, CustomValidatorRequestContext requestContext)
    {
        // Do this nullish check because the base request validator currently is not
        // strict null checking. Once that gets fixed then we can see about making
        // the user not nullish checked. If they are null then the validator should fail.
        if (user == null)
        {
            FillRequestContextWithErrorData(requestContext, "no_user", _noUserMessage);
            return false;
        }

        Version? clientVersion = currentContext.ClientVersion;

        // Deny access if the client version headers are missing.
        // We want to establish a strict contract with clients that if they omit this header,
        // then the server cannot guarantee that a client won't do harm to a user's data
        // with stale encryption architecture.
        if (clientVersion == null)
        {
            FillRequestContextWithErrorData(requestContext, "version_header_missing", _versionHeaderMissing);
            return false;
        }

        // Determine the minimum version client that a user needs. If no V2 encryption detected then
        // no validation needs to occur, which is why min version number can be null.
        Version? minVersion = user.HasV2Encryption() ? Constants.MinimumClientVersionForV2Encryption : null;

        // If min version is null then we know that the user had an encryption
        // configuration that doesn't require a minimum version. Allowing through.
        if (minVersion == null)
        {
            return true;
        }

        if (clientVersion < minVersion)
        {
            FillRequestContextWithErrorData(requestContext, "invalid_client_version", _upgradeMessage);
            return false;
        }

        return true;
    }

    private void FillRequestContextWithErrorData(
        CustomValidatorRequestContext requestContext,
        string errorId,
        string errorMessage)
    {
        requestContext.ValidationErrorResult = new ValidationResult
        {
            Error = errorId,
            ErrorDescription = errorMessage,
            IsError = true
        };
        requestContext.CustomResponse = new Dictionary<string, object>
        {
            { "ErrorModel", new ErrorResponseModel(errorMessage) }
        };
    }
}


