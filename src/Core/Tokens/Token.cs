using System;
using Bit.Core.Models.Business;
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

        public Token ProtectWith(IDataProtector dataProtector) =>
            new(dataProtector.Protect(ToString()));

        public Token UnprotectWith(IDataProtector dataProtector) =>
            new(dataProtector.Unprotect(ToString()));

        public Token ProtectWith(string key) =>
            new(SymmetricKeyProtectedString.Encrypt(ToString(), key).EncryptedString);

        /// <summary>
        /// Decrypts token with provided key
        /// </summary>
        /// <param name="key">The key to use to decrypt</param>
        /// <returns>A token populated with the decrypted string</returns>
        /// <exception>Throw Exception if decryption fails</exception>
        public Token UnprotectWith(string key)
        {
            var decrypted = new SymmetricKeyProtectedString(ToString()).Decrypt(key);

            if (decrypted == null)
            {
                throw new Exception("Incorrect key provided to decrypt token");
            }

            return new(decrypted);
        }

        public override string ToString() => _token;
    }
}
