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
        [StringLength(300)]
        public string Name { get; set; }
        public IEnumerable<string> CollectionIds { get; set; }

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
            return existingGroup;
        }
    }
}
