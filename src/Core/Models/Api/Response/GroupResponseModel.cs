using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class GroupResponseModel : ResponseModel
    {
        public GroupResponseModel(Group group, string obj = "group")
            : base(obj)
        {
            if(group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            Id = group.Id.ToString();
            OrganizationId = group.OrganizationId.ToString();
            Name = group.Name;
        }

        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public string Name { get; set; }
    }
}
