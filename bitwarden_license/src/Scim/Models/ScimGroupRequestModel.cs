using System;
using Bit.Core.Entities;

namespace Bit.Scim.Models
{
    public class ScimGroupRequestModel : BaseScimGroupModel
    {
        public ScimGroupRequestModel()
            : base(false)
        { }

        public Group ToGroup(Guid organizationId)
        {
            return new Group
            {
                Name = DisplayName,
                ExternalId = ExternalId,
                OrganizationId = organizationId
            };
        }
    }
}
