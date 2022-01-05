using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Tokens
{
    public class Token
    {
        private readonly string _token;

        public Token(string token)
        {
            _token = token;
        }

        public Token WithPrefix(string clearPrefix)
        {
            return new Token($"{clearPrefix}{_token}");
        }

        public Token RemovePrefix(string expectedClearPrefix)
        {
            if (!_token.StartsWith(expectedClearPrefix))
            {
                throw new BadTokenException("Missing clear text prefix");
            }

            return new Token(_token[expectedClearPrefix.Length..]);
        }

        public Token ProtectWith(IDataProtector dataProtector) =>
            new(dataProtector.Protect(ToString()));

        public override string ToString() => _token;
    }
}
