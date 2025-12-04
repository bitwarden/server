using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Models.Api;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

public interface IClientVersionValidator
{
    Task<bool> ValidateAsync(User user, CustomValidatorRequestContext requestContext);
}

/// <summary>
/// This validator will use the Client Version on a request, which currently maps
/// to the "Bitwarden-Client-Version" header, to determine if a user meets minimum
/// required client version for issuing tokens on an old client. This is done to
/// incentivize users getting on an updated client when their password encryption
/// method has already been updated. Currently this validator looks for the version
/// defined by MinimumClientVersionForV2Encryption.
///
/// If the header is omitted, then the validator returns that this request is valid.
/// </summary>
public class ClientVersionValidator(
    ICurrentContext currentContext,
    IGetMinimumClientVersionForUserQuery getMinimumClientVersionForUserQuery)
    : IClientVersionValidator
{
    private const string _upgradeMessage = "Please update your app to continue using Bitwarden";

    public async Task<bool> ValidateAsync(User? user, CustomValidatorRequestContext requestContext)
    {
        // Do this nullish check because the base request validator currently is not
        // strict null checking. Once that gets fixed then we can see about making
        // the user not nullish checked. If they are null then the validator should fail.
        if (user == null)
        {
            return false;
        }

        Version? clientVersion = currentContext.ClientVersion;
        Version? minVersion = await getMinimumClientVersionForUserQuery.Run(user);

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


