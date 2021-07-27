using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class ProfileResponseModel : ResponseModel
    {
        public ProfileResponseModel(User user,
<<<<<<< HEAD
            IEnumerable<OrganizationUserOrganizationDetails> organizationsUserDetails, bool twoFactorEnabled)
            : base("profile")
=======
            IEnumerable<OrganizationUserOrganizationDetails> organizationsUserDetails,
            IEnumerable<ProviderUserProviderDetails> providerUserDetails,
            IEnumerable<ProviderUserOrganizationDetails> providerUserOrganizationDetails,
            bool twoFactorEnabled) : base("profile")
>>>>>>> 545d5f942b1a2d210c9488c669d700d01d2c1aeb
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Id = user.Id.ToString();
            Name = user.Name;
            Email = user.Email;
            EmailVerified = user.EmailVerified;
            Premium = user.Premium;
            MasterPasswordHint = string.IsNullOrWhiteSpace(user.MasterPasswordHint) ? null : user.MasterPasswordHint;
            Culture = user.Culture;
            TwoFactorEnabled = twoFactorEnabled;
            Key = user.Key;
            PrivateKey = user.PrivateKey;
            SecurityStamp = user.SecurityStamp;
            ForcePasswordReset = user.ForcePasswordReset;
            Organizations = organizationsUserDetails?.Select(o => new ProfileOrganizationResponseModel(o));
<<<<<<< HEAD
=======
            Providers = providerUserDetails?.Select(p => new ProfileProviderResponseModel(p));
            ProviderOrganizations =
                providerUserOrganizationDetails?.Select(po => new ProfileProviderOrganizationResponseModel(po));
>>>>>>> 545d5f942b1a2d210c9488c669d700d01d2c1aeb
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool EmailVerified { get; set; }
        public bool Premium { get; set; }
        public string MasterPasswordHint { get; set; }
        public string Culture { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string Key { get; set; }
        public string PrivateKey { get; set; }
        public string SecurityStamp { get; set; }
        public bool ForcePasswordReset { get; set; }
        public IEnumerable<ProfileOrganizationResponseModel> Organizations { get; set; }
<<<<<<< HEAD
=======
        public IEnumerable<ProfileProviderResponseModel> Providers { get; set; }
        public IEnumerable<ProfileProviderOrganizationResponseModel> ProviderOrganizations { get; set; }
>>>>>>> 545d5f942b1a2d210c9488c669d700d01d2c1aeb
    }
}
