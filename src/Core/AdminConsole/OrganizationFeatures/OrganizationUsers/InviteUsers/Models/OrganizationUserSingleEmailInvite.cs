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
    public bool AccessSecretsManager { get; init; }

    public OrganizationUserSingleEmailInvite(string email,
        IEnumerable<CollectionAccessSelection> accessibleCollections,
        OrganizationUserType type,
        Permissions permissions,
        bool accessSecretsManager)
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
        AccessSecretsManager = accessSecretsManager;
    }
}
