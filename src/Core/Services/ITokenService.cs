using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Services
{
    public interface IProtectedData
    {
        public void Deserialize(string message);
        public string Serialize();
    }

    public interface ITokenService<T> where T: IProtectedData
    {
        public string Protect(IDataProtector dataProtector, T data);
        public T Unprotect(IDataProtector dataProtector, string message);
    }
}
