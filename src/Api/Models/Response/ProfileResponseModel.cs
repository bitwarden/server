using System;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class ProfileResponseModel : ResponseModel
    {
        public ProfileResponseModel(User user)
            : base("profile")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Id = user.Id;
            Name = user.Name;
            Email = user.Email;
            MasterPasswordHint = string.IsNullOrWhiteSpace(user.MasterPasswordHint) ? null : user.MasterPasswordHint;
            Culture = user.Culture;
            TwoFactorEnabled = user.TwoFactorEnabled;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string MasterPasswordHint { get; set; }
        public string Culture { get; set; }
        public bool TwoFactorEnabled { get; set; }
    }
}
