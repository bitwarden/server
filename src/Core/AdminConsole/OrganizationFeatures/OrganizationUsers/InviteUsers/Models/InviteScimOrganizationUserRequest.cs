using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public record InviteScimOrganizationUserRequest : OrganizationUserSingleEmailInvite
{
    public InviteOrganization InviteOrganization { get; private init; }
    public DateTimeOffset PerformedAt { get; private init; }
    public string ExternalId { get; private init; } = string.Empty;

    public InviteScimOrganizationUserRequest(string email,
        bool hasSecretsManager,
        InviteOrganization inviteOrganization,
        DateTimeOffset performedAt,
        string externalId) : base(
        email: email,
        accessibleCollections: [],
        type: OrganizationUserType.User,
        permissions: new Permissions(),
        accessSecretsManager: hasSecretsManager)
    {
        InviteOrganization = inviteOrganization;
        PerformedAt = performedAt;
        ExternalId = externalId;
    }
}
