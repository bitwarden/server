using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserSubvaultResponseModel : ResponseModel
    {
        public OrganizationUserSubvaultResponseModel(SubvaultUserSubvaultDetails details,
            string obj = "organizationUserSubvault")
            : base(obj)
        {
            if(details == null)
            {
                throw new ArgumentNullException(nameof(details));
            }

            Id = details.Id.ToString();
            Name = details.Name;
            SubvaultId = details.SubvaultId.ToString();
            ReadOnly = details.ReadOnly;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string SubvaultId { get; set; }
        public bool ReadOnly { get; set; }
    }
}
