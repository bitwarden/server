using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tokens;

public class DataProtectorTokenFactory<T> : IDataProtectorTokenFactory<T> where T : Tokenable
{
    private readonly IDataProtector _dataProtector;
    private readonly string _clearTextPrefix;
    private readonly ILogger<DataProtectorTokenFactory<T>> _logger;

    public DataProtectorTokenFactory(string clearTextPrefix, string purpose, IDataProtectionProvider dataProtectionProvider, ILogger<DataProtectorTokenFactory<T>> logger)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(purpose);
        _clearTextPrefix = clearTextPrefix;
        _logger = logger;
    }

    public string Protect(T data) =>
        data.ToToken().ProtectWith(_dataProtector, _logger).WithPrefix(_clearTextPrefix).ToString();

    /// <summary>
    /// Unprotect token
    /// </summary>
    /// <param name="token">The token to parse</param>
    /// <returns>The parsed tokenable</returns>
    /// <exception>Throws CryptographicException if fails to unprotect</exception>
    public T Unprotect(string token) =>
        Tokenable.FromToken<T>(new Token(token).RemovePrefix(_clearTextPrefix).UnprotectWith(_dataProtector, _logger).ToString());

    public bool TokenValid(string token)
    {
        try
        {
            return Unprotect(token).Valid;
        }
        catch
        {
            return false;
        }
    }

    public bool TryUnprotect(string token, out T data)
    {
        try
        {
            data = Unprotect(token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to unprotect token: {rawToken}", token);
            data = default;
            return false;
        }
    }
}
