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

public class ClientVersionValidator(
    ICurrentContext currentContext,
    IGetMinimumClientVersionForUserQuery getMinimumClientVersionForUserQuery)
    : IClientVersionValidator
{
    private const string _upgradeMessage = "Please update your app to continue using Bitwarden";

    public async Task<bool> ValidateAsync(User? user, CustomValidatorRequestContext requestContext)
    {
        if (user == null)
        {
            return true;
        }

        Version clientVersion = currentContext.ClientVersion;
        Version? minVersion = await getMinimumClientVersionForUserQuery.Run(user);

        // Allow through if headers are missing.
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


