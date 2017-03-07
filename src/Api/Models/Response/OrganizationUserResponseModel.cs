using System;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.Models
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
        }

        public string Id { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public OrganizationUserType Type { get; set; }
        public OrganizationUserStatusType Status { get; set; }
    }
}
