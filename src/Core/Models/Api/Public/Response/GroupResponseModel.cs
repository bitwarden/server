using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Public
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

            Id = group.Id;
            OrganizationId = group.OrganizationId;
            Name = group.Name;
            AccessAll = group.AccessAll;
            ExternalId = group.ExternalId;
        }

        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; }
        public bool AccessAll { get; set; }
        public string ExternalId { get; set; }
    }
}
