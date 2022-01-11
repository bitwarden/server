using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Tokens
{
    public class DataProtectorTokenFactory<T> : IDataProtectorTokenFactory<T> where T : Tokenable
    {
        private readonly IDataProtector _dataProtector;
        private readonly string _clearTextPrefix;

        public DataProtectorTokenFactory(string clearTextPrefix, string purpose, IDataProtectionProvider dataProtectionProvider)
        {
            _dataProtector = dataProtectionProvider.CreateProtector(purpose);
            _clearTextPrefix = clearTextPrefix;
        }

        public string Protect(T data) =>
            data.ToToken().ProtectWith(_dataProtector).WithPrefix(_clearTextPrefix).ToString();

        public T Unprotect(string token) =>
            Tokenable.FromToken<T>(new Token(token).RemovePrefix(_clearTextPrefix).UnprotectWith(_dataProtector).ToString());

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
            catch
            {
                data = default;
                return false;
            }
        }
    }
}
