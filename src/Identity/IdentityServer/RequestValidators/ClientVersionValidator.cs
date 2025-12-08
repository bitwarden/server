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

        // Determine the minimum version client that a user needs. If no V2 encryption detected then
        // no validation needs to occur, which is why min version number can be null.
        Version? minVersion = user.HasV2Encryption() ? Constants.MinimumClientVersionForV2Encryption : null;

        // Deny access if the client version headers are missing.
        // We want to establish a contract with clients that if they omit this heading that they
        // will be susceptible to encryption failures.
        if (clientVersion == null)
        {
            requestContext.ValidationErrorResult = new ValidationResult
            {
                Error = "version_header_missing",
                ErrorDescription = _versionHeaderMissing,
                IsError = true
            };
            requestContext.CustomResponse = new Dictionary<string, object>
            {
                { "ErrorModel", new ErrorResponseModel(_versionHeaderMissing) }
            };
            return false;
        }

        // If min version is null then we know that the user had an encryption
        // configuration that doesn't require a minimum version. Allowing through.
        if (minVersion == null)
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


