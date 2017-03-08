using System;
using Bit.Core.Domains;

namespace Bit.Api.Models
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
