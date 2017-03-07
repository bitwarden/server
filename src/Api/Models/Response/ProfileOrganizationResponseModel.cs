using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.Models
{
    public class ProfileOrganizationResponseModel : ResponseModel
    {
        public ProfileOrganizationResponseModel(OrganizationUserOrganizationDetails organization)
            : base("profileOrganization")
        {
            Id = organization.OrganizationId.ToString();
            Name = organization.Name;
            Key = organization.Key;
            Status = organization.Status;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public OrganizationUserStatusType Status { get; set; }
    }
}
