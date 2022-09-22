using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tokens;

public class Token
{
    private readonly string _token;

    public Token(string token)
    {
        _token = token;
    }

    public Token WithPrefix(string prefix)
    {
        return new Token($"{prefix}{_token}");
    }

    public Token RemovePrefix(string expectedPrefix)
    {
        if (!_token.StartsWith(expectedPrefix))
        {
            throw new BadTokenException($"Expected prefix, {expectedPrefix}, was not present.");
        }

        return new Token(_token[expectedPrefix.Length..]);
    }


    public Token ProtectWith<T>(IDataProtector dataProtector, ILogger<T> logger)
    {
        logger.LogDebug("Protecting token: {0}", this);
        return new(dataProtector.Protect(ToString()));
    }

    public Token UnprotectWith<T>(IDataProtector dataProtector, ILogger<T> logger)
    {
        var unprotected = "";
        try
        {
            unprotected = dataProtector.Unprotect(ToString());
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "Failed to unprotect token: {0}", this);
            throw;
        }
        logger.LogDebug("Unprotected token: {0} to {1}", this, unprotected);
        return new(dataProtector.Unprotect(ToString()));
    }

    public override string ToString() => _token;
}
