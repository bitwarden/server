using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.InviteOrganizationUserErrorMessages;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteOrganizationUserFunctions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public record OrganizationUserSingleEmailInvite
{
    public string Email { get; init; } = string.Empty;
    public CollectionAccessSelection[] AccessibleCollections { get; init; } = [];
    public Permissions Permissions { get; init; } = new();
    public OrganizationUserType Type { get; init; } = OrganizationUserType.User;
    public InviteOrganization InviteOrganization { get; private init; }
    public DateTimeOffset PerformedAt { get; private init; }
    public string ExternalId { get; private init; } = string.Empty;

    public OrganizationUserSingleEmailInvite(string email,
        InviteOrganization inviteOrganization,
        DateTimeOffset performedAt,
        string externalId) : this(
        email: email,
        accessibleCollections: [],
        type: OrganizationUserType.User,
        permissions: new Permissions())
    {
        InviteOrganization = inviteOrganization;
        PerformedAt = performedAt;
        ExternalId = externalId;
    }

    public OrganizationUserSingleEmailInvite(string email,
        IEnumerable<CollectionAccessSelection> accessibleCollections,
        OrganizationUserType type,
        Permissions permissions)
    {
        if (!email.IsValidEmail())
        {
            throw new BadRequestException(InvalidEmailErrorMessage);
        }

        if (accessibleCollections?.Any(ValidateCollectionConfiguration) ?? false)
        {
            throw new BadRequestException(InvalidCollectionConfigurationErrorMessage);
        }

        Email = email;
        AccessibleCollections = accessibleCollections.ToArray();
        Type = type;
        Permissions = permissions;
    }
}
