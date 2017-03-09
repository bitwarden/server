using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class SubvaultCreateRequestModel : SubvaultUpdateRequestModel
    {
        public string OrganizationId { get; set; }

        public Subvault ToSubvault()
        {
            return ToSubvault(new Subvault
            {
                OrganizationId = new Guid(OrganizationId)
            });
        }
    }

    public class SubvaultUpdateRequestModel
    {
        [Required]
        [EncryptedString]
        [StringLength(300)]
        public string Name { get; set; }

        public Subvault ToSubvault(Subvault existingSubvault)
        {
            existingSubvault.Name = Name;
            return existingSubvault;
        }
    }
}
