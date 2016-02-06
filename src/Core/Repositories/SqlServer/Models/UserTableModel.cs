using System;
using Bit.Core.Domains;
using Bit.Core.Enums;

namespace Bit.Core.Repositories.SqlServer.Models
{
    public class UserTableModel : ITableModel<User>
    {
        public UserTableModel() { }

        public UserTableModel(User user)
        {
            Id = new Guid(user.Id);
            Name = user.Name;
            Email = user.Email;
            MasterPassword = user.MasterPassword;
            MasterPasswordHint = user.MasterPasswordHint;
            Culture = user.Culture;
            SecurityStamp = user.SecurityStamp;
            TwoFactorEnabled = user.TwoFactorEnabled;
            TwoFactorProvider = user.TwoFactorProvider;
            AuthenticatorKey = user.AuthenticatorKey;
            CreationDate = user.CreationDate;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string MasterPassword { get; set; }
        public string MasterPasswordHint { get; set; }
        public string Culture { get; set; }
        public string SecurityStamp { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public TwoFactorProvider? TwoFactorProvider { get; set; }
        public string AuthenticatorKey { get; set; }
        public DateTime CreationDate { get; set; }

        public User ToDomain()
        {
            return new User
            {
                Id = Id.ToString(),
                Name = Name,
                Email = Email,
                MasterPassword = MasterPassword,
                MasterPasswordHint = MasterPasswordHint,
                Culture = Culture,
                SecurityStamp = SecurityStamp,
                TwoFactorEnabled = TwoFactorEnabled,
                TwoFactorProvider = TwoFactorProvider,
                AuthenticatorKey = AuthenticatorKey,
                CreationDate = CreationDate
            };
        }
    }
}
