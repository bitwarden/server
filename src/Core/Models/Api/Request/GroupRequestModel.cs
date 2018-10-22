using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class GroupRequestModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        [Required]
        public bool? AccessAll { get; set; }
        [StringLength(300)]
        public string ExternalId { get; set; }
        public IEnumerable<SelectionReadOnlyRequestModel> Collections { get; set; }

        public Group ToGroup(Guid orgId)
        {
            return ToGroup(new Group
            {
                OrganizationId = orgId
            });
        }

        public Group ToGroup(Group existingGroup)
        {
            existingGroup.Name = Name;
            existingGroup.AccessAll = AccessAll.Value;
            existingGroup.ExternalId = ExternalId;
            return existingGroup;
        }
    }
}
