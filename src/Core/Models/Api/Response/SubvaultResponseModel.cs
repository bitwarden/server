using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class SubvaultResponseModel : ResponseModel
    {
        public SubvaultResponseModel(Subvault subvault)
            : base("subvault")
        {
            if(subvault == null)
            {
                throw new ArgumentNullException(nameof(subvault));
            }

            Id = subvault.Id.ToString();
            OrganizationId = subvault.OrganizationId.ToString();
            Name = subvault.Name;
        }

        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string Name { get; set; }
    }
}
