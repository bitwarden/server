
using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables
{
    public class SsoTokenable : ExpiringTokenable
    {
        private const double _tokenLifetimeInSeconds = 30;
        public const string ClearTextPrefix = "BWUserPrefix_";
        public const string DataProtectorPurpose = "SsoTokenDataProtector";
        public const string TokenIdentifier = "SsoTokenIdentifier";

        public string Identifier { get; set; } = TokenIdentifier;
        public SsoToken Token { get; set; }

        [JsonConstructor]
        public SsoTokenable()
        {
            ExpirationDate = DateTime.UtcNow.AddSeconds(_tokenLifetimeInSeconds);
        }

        public SsoTokenable(SsoToken token) : this()
        {
            Token = token;
        }

        public bool TokenIsValid(SsoToken token)
        {
            return token.DomainHint.Equals(Token.DomainHint, StringComparison.InvariantCultureIgnoreCase)
                && token.OrganizationId.Equals(Token.OrganizationId);
        }

        // Validates deserialized 
        protected override bool TokenIsValid() =>
            Identifier == TokenIdentifier
            && Token.OrganizationId != Guid.Empty
            && !string.IsNullOrWhiteSpace(Token.DomainHint);
    }
}
