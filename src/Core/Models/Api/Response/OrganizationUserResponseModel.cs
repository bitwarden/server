using System;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserResponseModel : ResponseModel
    {
        public OrganizationUserResponseModel(OrganizationUserUserDetails organizationUser, string obj = "organizationUser")
            : base(obj)
        {
            if(organizationUser == null)
            {
                throw new ArgumentNullException(nameof(organizationUser));
            }

            Id = organizationUser.Id.ToString();
            UserId = organizationUser.UserId?.ToString();
            Name = organizationUser.Name;
            Email = organizationUser.Email;
            Type = organizationUser.Type;
            Status = organizationUser.Status;
            AccessAll = organizationUser.AccessAll;
        }

        public string Id { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public OrganizationUserType Type { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public bool AccessAll { get; set; }
    }

    public class OrganizationUserDetailsResponseModel : OrganizationUserResponseModel
    {
        public OrganizationUserDetailsResponseModel(OrganizationUserUserDetails organizationUser,
            IEnumerable<CollectionUserCollectionDetails> collections)
            : base(organizationUser, "organizationUserDetails")
        {
            Collections = new ListResponseModel<OrganizationUserCollectionResponseModel>(
                collections.Select(c => new OrganizationUserCollectionResponseModel(c)));
        }

        public ListResponseModel<OrganizationUserCollectionResponseModel> Collections { get; set; }
    }
}
