﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Utilities;

namespace Bit.Scim.Models;

public class ScimGroupRequestModel : BaseScimGroupModel
{
    public ScimGroupRequestModel()
        : base(false)
    { }

    public Group ToGroup(Guid organizationId)
    {
        var externalId = string.IsNullOrWhiteSpace(ExternalId) ? CoreHelpers.RandomString(15) : ExternalId;
        return new Group
        {
            Name = DisplayName,
            ExternalId = externalId,
            OrganizationId = organizationId
        };
    }

    public List<GroupMembersModel> Members { get; set; }

    public class GroupMembersModel
    {
        public string Value { get; set; }
        public string Display { get; set; }
    }
}
