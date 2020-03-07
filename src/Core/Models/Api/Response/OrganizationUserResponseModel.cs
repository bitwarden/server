using System;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserResponseModel : ResponseModel
    {
        public OrganizationUserResponseModel(OrganizationUser organizationUser, string obj = "organizationUser")
            : base(obj)
        {
            if(organizationUser == null)
            {
                throw new ArgumentNullException(nameof(organizationUser));
            }

            Id = organizationUser.Id.ToString();
            UserId = organizationUser.UserId?.ToString();
            Type = organizationUser.Type;
            Status = organizationUser.Status;
            AccessAll = organizationUser.AccessAll;
        }

        public OrganizationUserResponseModel(OrganizationUserUserDetails organizationUser, string obj = "organizationUser")
            : base(obj)
        {
            if(organizationUser == null)
            {
                throw new ArgumentNullException(nameof(organizationUser));
            }

            Id = organizationUser.Id.ToString();
            UserId = organizationUser.UserId?.ToString();
            Type = organizationUser.Type;
            Status = organizationUser.Status;
            AccessAll = organizationUser.AccessAll;
        }

        public string Id { get; set; }
        public string UserId { get; set; }
        public OrganizationUserType Type { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public bool AccessAll { get; set; }
    }

    public class OrganizationUserDetailsResponseModel : OrganizationUserResponseModel
    {
        public OrganizationUserDetailsResponseModel(OrganizationUser organizationUser,
            IEnumerable<SelectionReadOnly> collections)
            : base(organizationUser, "organizationUserDetails")
        {
            Collections = collections.Select(c => new SelectionReadOnlyResponseModel(c));
        }

        public IEnumerable<SelectionReadOnlyResponseModel> Collections { get; set; }
    }

    public class OrganizationUserUserDetailsResponseModel : OrganizationUserResponseModel
    {
        public OrganizationUserUserDetailsResponseModel(OrganizationUserUserDetails organizationUser,
            bool twoFactorEnabled, string obj = "organizationUserUserDetails")
            : base(organizationUser, obj)
        {
            if(organizationUser == null)
            {
                throw new ArgumentNullException(nameof(organizationUser));
            }

            Name = organizationUser.Name;
            Email = organizationUser.Email;
            TwoFactorEnabled = twoFactorEnabled;
        }

        public string Name { get; set; }
        public string Email { get; set; }
        public bool TwoFactorEnabled { get; set; }
    }
}
