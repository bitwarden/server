﻿using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
#nullable enable

public class DeleteUserValidationRequest
{
    public Guid OrganizationId { get; init; }
    public OrganizationUser? OrganizationUser { get; init; }
    public User? User { get; init; }
    public Guid? DeletingUserId { get; init; }

    public IDictionary<Guid, bool>? ManagementStatus { get; init; }
    public bool? IsManaged { get; init; }
}
