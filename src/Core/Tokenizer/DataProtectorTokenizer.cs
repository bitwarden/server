using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Tokenizer
{
    public class DataProtectorTokenizer<T> : TokenizerBase<T> where T : ITokenable
    {
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public DataProtectorTokenizer(string clearTextPrefix, IDataProtectionProvider dataProtectionProvider) : base(clearTextPrefix)
        {
            _dataProtectionProvider = dataProtectionProvider;
        }

        protected override string ProtectData(string key, T data)
        {
            return _dataProtectionProvider.CreateProtector(key).Protect(Serialize(data));
        }

        protected override T UnprotectData(string key, string protectedData)
        {
            return Deserialize(_dataProtectionProvider.CreateProtector(key).Unprotect(protectedData));
        }
    }
}
