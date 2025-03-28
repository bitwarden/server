using Bit.Core.Context;
using Bit.Core.Utilities;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class AuthRequestHeaderValidator : IAuthRequestHeaderValidator
{
    private readonly ICurrentContext _currentContext;

    public AuthRequestHeaderValidator(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    public bool ValidateAuthEmailHeader(string userEmail)
    {
        if (_currentContext.HttpContext.Request.Headers.TryGetValue("Auth-Email", out var authEmailHeader))
        {
            try
            {
                var authEmailDecoded = CoreHelpers.Base64UrlDecodeString(authEmailHeader);
                if (authEmailDecoded != userEmail)
                {
                    return false;
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is FormatException)
            {
                // Invalid B64 encoding
                return false;
            }
        }
        else
        {
            return false;
        }
        return true;
    }
}
