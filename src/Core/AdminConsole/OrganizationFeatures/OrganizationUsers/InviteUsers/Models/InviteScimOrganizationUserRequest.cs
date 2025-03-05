using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public record InviteScimOrganizationUserRequest : OrganizationUserSingleEmailInvite
{
    public OrganizationDto Organization { get; private init; }
    public DateTimeOffset PerformedAt { get; private init; }
    public string ExternalId { get; private init; } = string.Empty;

    public InviteScimOrganizationUserRequest(string email,
        bool hasSecretsManager,
        OrganizationDto organization,
        DateTimeOffset performedAt,
        string externalId) : base(
        email: email,
        accessibleCollections: [],
        type: OrganizationUserType.User,
        permissions: new Permissions(),
        accessSecretsManager: hasSecretsManager)
    {
        Organization = organization;
        PerformedAt = performedAt;
        ExternalId = externalId;
    }
}
