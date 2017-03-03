using System;
using Bit.Core.Domains;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Api.Models
{
    public class ProfileResponseModel : ResponseModel
    {
        public ProfileResponseModel(User user, IEnumerable<Organization> organizations)
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
            Organizations = organizations?.Select(o => new OrganizationResponseModel(o));
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string MasterPasswordHint { get; set; }
        public string Culture { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public IEnumerable<OrganizationResponseModel> Organizations { get; set; }

        public class OrganizationResponseModel
        {
            public OrganizationResponseModel(Organization organization)
            {
                Id = organization.Id.ToString();
                Name = organization.Name;
            }

            public string Id { get; set; }
            public string Name { get; set; }
        }
    }
}
