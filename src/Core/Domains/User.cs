using System;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Domains
{
    public class User : IDataObject<Guid>
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool EmailVerified { get; set; }
        public string MasterPassword { get; set; }
        public string MasterPasswordHint { get; set; }
        public string Culture { get; set; } = "en-US";
        public string SecurityStamp { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public TwoFactorProvider? TwoFactorProvider { get; set; }
        public string AuthenticatorKey { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
