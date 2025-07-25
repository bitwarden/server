using Bit.Core.Tools.Models.Data;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public interface ISendPasswordRequestValidator
{
    /// <summary>
    /// Validates the send password hash against the client hashed password.
    /// If this method fails then it will automatically set the context.Result to an invalid grant result.
    /// </summary>
    /// <param name="context">request context</param>
    /// <param name="resourcePassword">resource password authentication method containing the hash of the Send being retrieved</param>
    /// <returns>true if password hashes match, false otherwise.</returns>
    bool ValidateSendPassword(ExtensionGrantValidationContext context, ResourcePassword resourcePassword);
}
