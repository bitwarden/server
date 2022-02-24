namespace Bit.Core.Tokens
{
    public class SymmetricKeyProtectedTokenFactory<T> : ISymmetricKeyProtectedTokenFactory<T> where T : Tokenable
    {
        private string _clearTextPrefix;

        public SymmetricKeyProtectedTokenFactory(string clearTextPrefix)
        {
            _clearTextPrefix = clearTextPrefix;
        }

        public string Protect(string key, T data) =>
            data.ToToken().ProtectWith(key).WithPrefix(_clearTextPrefix).ToString();
        public T Unprotect(string key, string token) =>
            Tokenable.FromToken<T>(new Token(token).RemovePrefix(_clearTextPrefix).UnprotectWith(key).ToString());

        public bool TokenValid(string key, string token)
        {
            try
            {
                return Unprotect(key, token).Valid;
            }
            catch
            {
                return false;
            }
        }
        public bool TryUnprotect(string key, string token, out T data)
        {
            try
            {
                data = Unprotect(key, token);
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
