using System.Text.Json;

namespace Bit.Core.Tokenizer
{
    public abstract class TokenizerBase<T> : ITokenizer<T> where T : ITokenable
    {
        private readonly string _clearTextPrefix;

        protected TokenizerBase(string clearTextPrefix)
        {
            _clearTextPrefix = clearTextPrefix ?? "";
        }

        public string Protect(string key, T data)
        {
            return $"{_clearTextPrefix}{ProtectData(key, data)}";
        }

        public T Unprotect(string key, string token)
        {
            var strippedProtectedData = StripClearTextPrefix(token);
            return UnprotectData(key, strippedProtectedData);
        }

        public bool TryUnprotect(string key, string token, out T data)
        {
            data = default;
            try
            {
                data = Unprotect(key, token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TokenValid(string key, string token)
        {
            return Unprotect(key, token).Valid;
        }

        protected string Serialize(T data)
        {
            return JsonSerializer.Serialize(data);
        }

        protected T Deserialize(string protectedData)
        {
            return JsonSerializer.Deserialize<T>(protectedData);
        }

        private string StripClearTextPrefix(string protectedData)
        {
            if (!protectedData.StartsWith(_clearTextPrefix))
            {
                throw new BadTokenException("Missing clear text prefix");
            }

            return protectedData[_clearTextPrefix.Length..];
        }

        protected abstract string ProtectData(string key, T data);
        protected abstract T UnprotectData(string key, string protectedData);
    }
}
