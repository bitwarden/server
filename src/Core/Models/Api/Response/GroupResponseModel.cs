using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Data;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class GroupResponseModel : ResponseModel
    {
        public GroupResponseModel(Group group, string obj = "group")
            : base(obj)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            Id = group.Id.ToString();
            OrganizationId = group.OrganizationId.ToString();
            Name = group.Name;
            AccessAll = group.AccessAll;
            ExternalId = group.ExternalId;
        }

        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string Name { get; set; }
        public bool AccessAll { get; set; }
        public string ExternalId { get; set; }
    }

    public class GroupDetailsResponseModel : GroupResponseModel
    {
        public GroupDetailsResponseModel(Group group, IEnumerable<SelectionReadOnly> collections)
            : base(group, "groupDetails")
        {
            Collections = collections.Select(c => new SelectionReadOnlyResponseModel(c));
        }

        public IEnumerable<SelectionReadOnlyResponseModel> Collections { get; set; }
    }
}
