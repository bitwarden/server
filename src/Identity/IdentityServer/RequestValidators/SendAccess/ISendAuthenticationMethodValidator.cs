using Bit.Core.Tools.Models.Data;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public interface ISendAuthenticationMethodValidator<T> where T : SendAuthenticationMethod
{
    /// <summary>
    /// </summary>
    /// <param name="context">request context</param>
    /// <param name="authMethod">SendAuthenticationRecord that contains the information to be compared against the context</param>
    /// <param name="sendId">the sendId being accessed</param>
    /// <returns>returns the result of the validation; A failed result will be an error a successful will contain the claims and a success</returns>
    Task<GrantValidationResult> ValidateRequestAsync(ExtensionGrantValidationContext context, T authMethod, Guid sendId);
}
