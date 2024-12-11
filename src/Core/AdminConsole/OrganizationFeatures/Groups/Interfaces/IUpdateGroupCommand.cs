﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;

public interface IUpdateGroupCommand
{
    Task UpdateGroupAsync(
        Group group,
        Organization organization,
        ICollection<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> users = null
    );

    Task UpdateGroupAsync(
        Group group,
        Organization organization,
        EventSystemUser systemUser,
        ICollection<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> users = null
    );
}
