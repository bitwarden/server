using System;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Tokenizer
{
    public class TokenizerFactory : ITokenizerFactory
    {
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public TokenizerFactory(IDataProtectionProvider dataProtectionProvider)
        {
            _dataProtectionProvider = dataProtectionProvider;
        }

        public ITokenizer<T> Create<T>(string clearTextPrefix, TokenType targetTokenType) where T : ITokenable
        {
            switch (targetTokenType)
            {
                case TokenType.DataProtector:
                    return new DataProtectorTokenizer<T>(clearTextPrefix, _dataProtectionProvider);
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetTokenType), targetTokenType, null);
            }
        }
    }
}
