using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
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
            Type = organization.Type;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public OrganizationUserType Type { get; set; }
    }
}
