using System;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Scim.Models
{
    public class ScimGroup : ScimResource
    {
        public ScimGroup() { }

        public ScimGroup(Group group)
        {
            Id = group.Id.ToString();
            ExternalId = group.ExternalId;
            DisplayName = group.Name;
            Meta = new ScimResourceMetadata("Group");
        }

        public override string SchemaIdentifier => Constants.Schemas.Group;
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
        [JsonProperty("members")]
        public IEnumerable<ScimMultiValuedAttribute> Members { get; set; }

        public Group ToGroup(Guid orgId)
        {
            return new Group
            {
                ExternalId = ExternalId,
                Name = DisplayName,
                OrganizationId = orgId
            };
        }

        public Group ToGroup(Group group)
        {
            group.Name = DisplayName;
            return group;
        }
    }
}
