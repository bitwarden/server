using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class SubvaultRequestModel
    {
        [Required]
        [EncryptedString]
        [StringLength(300)]
        public string Name { get; set; }

        public Subvault ToSubvault(Guid orgId)
        {
            return ToSubvault(new Subvault
            {
                OrganizationId = orgId
            });
        }

        public Subvault ToSubvault(Subvault existingSubvault)
        {
            existingSubvault.Name = Name;
            return existingSubvault;
        }
    }
}
