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
    private static readonly string UpgradeMessage = "Please update your app to continue using Bitwarden";

    public async Task<bool> ValidateAsync(User? user, CustomValidatorRequestContext requestContext)
    {
        if (user == null)
        {
            return true;
        }

        var clientVersion = currentContext.ClientVersion;
        var minVersion = await getMinimumClientVersionForUserQuery.Run(user);

        // Fail-open if headers are missing or no restriction
        if (minVersion == null)
        {
            return true;
        }

        if (clientVersion < minVersion)
        {
            requestContext.ValidationErrorResult = new ValidationResult
            {
                Error = "invalid_client_version",
                ErrorDescription = UpgradeMessage,
                IsError = true
            };
            requestContext.CustomResponse = new Dictionary<string, object>
            {
                { "ErrorModel", new ErrorResponseModel(UpgradeMessage) }
            };
            return false;
        }

        return true;
    }
}


