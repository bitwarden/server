using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class OrganizationUserInviteDto
{
    public string Email { get; private init; } = string.Empty;
    public Guid[] AccessibleCollections { get; private init; } = [];
    public string ExternalId { get; private init; } = string.Empty;
    public Permissions Permissions { get; private init; } = new();
    public OrganizationUserType Type { get; private init; } = OrganizationUserType.User;
    public bool AccessSecretsManager { get; private init; }

    public static OrganizationUserInviteDto Create(string email, OrganizationUserInvite invite)
    {
        return new OrganizationUserInviteDto
        {
            Email = email,
            AccessibleCollections = invite.AccessibleCollections,
            ExternalId = invite.ExternalId,
            Type = invite.Type,
            Permissions = invite.Permissions,
            AccessSecretsManager = invite.AccessSecretsManager
        };
    }
}
