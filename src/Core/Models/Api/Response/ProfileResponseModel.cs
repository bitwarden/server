using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class ProfileResponseModel : ResponseModel
    {
        public ProfileResponseModel(User user, IEnumerable<OrganizationUserOrganizationDetails> organizationsUserDetails)
            : base("profile")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Id = user.Id.ToString();
            Name = user.Name;
            Email = user.Email;
            MasterPasswordHint = string.IsNullOrWhiteSpace(user.MasterPasswordHint) ? null : user.MasterPasswordHint;
            Culture = user.Culture;
            TwoFactorEnabled = user.TwoFactorEnabled;
            Organizations = organizationsUserDetails?.Select(o => new ProfileOrganizationResponseModel(o));
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string MasterPasswordHint { get; set; }
        public string Culture { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public IEnumerable<ProfileOrganizationResponseModel> Organizations { get; set; }
    }
}
