using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Services
{
    public class TokenService<T> : ITokenService<T> where T : IProtectedData, new()
    {
        public string Protect(IDataProtector dataProtector, T data)
        {
            // TODO: Handle scenario where we'll want to add a unprotected identifier in front
            var s = data.Serialize();
            return dataProtector.Protect(s);
        }

        public T Unprotect(IDataProtector dataProtector, string message)
        {
            // TODO: Handle scenario where there is a unprotected identifier in front.
            var unprotectedMessage = dataProtector.Unprotect(message);

            var obj = new T();
            obj.Deserialize(unprotectedMessage);

            return obj;
        }
    }
}
