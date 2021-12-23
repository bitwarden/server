using System;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Business.Tokens
{
    public class EmergencyAccessInviteToken : Tokenizer.ExpiringToken
    {
        public string Identifier { get; set; }
        public Guid Id { get; set; }
        public string Email { get; set; }

        public bool IsValid(Guid id, string email)
        {
            return Id == id &&
                Email.Equals(email, StringComparison.InvariantCultureIgnoreCase);
        }

        protected override bool TokenIsValid() => Identifier == "EmergencyAccessInvite";

    }
}
