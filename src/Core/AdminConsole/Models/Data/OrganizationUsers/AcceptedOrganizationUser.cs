using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data.OrganizationUsers;

public class AcceptedOrganizationUser : OrganizationUser
{
    public AcceptedOrganizationUser(OrganizationUser organizationUser, string key) : this(organizationUser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    public AcceptedOrganizationUser(OrganizationUser organizationUser)
    {
        ArgumentNullException.ThrowIfNull(organizationUser);
        ArgumentNullException.ThrowIfNull(organizationUser.UserId);

        if (organizationUser.Status != OrganizationUserStatusType.Accepted)
        {
            throw new ArgumentException("The organization user must be accepted", nameof(organizationUser));
        }

        Id = organizationUser.Id;
        OrganizationId = organizationUser.OrganizationId;
        UserId = organizationUser.UserId.Value;
        Email = organizationUser.Email;
        Key = organizationUser.Key;
        Type = organizationUser.Type;
        ExternalId = organizationUser.ExternalId;
        CreationDate = organizationUser.CreationDate;
        RevisionDate = organizationUser.RevisionDate;
        Permissions = organizationUser.Permissions;
        ResetPasswordKey = organizationUser.ResetPasswordKey;
        AccessSecretsManager = organizationUser.AccessSecretsManager;
    }

    public new Guid UserId { get; init; }
}
