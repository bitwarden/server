using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.InviteOrganizationUserErrorMessages;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteOrganizationUserFunctions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class OrganizationUserSingleEmailInvite
{
    public string Email { get; private init; } = string.Empty;
    public CollectionAccessSelection[] AccessibleCollections { get; private init; } = [];
    public Permissions Permissions { get; private init; } = new();
    public OrganizationUserType Type { get; private init; } = OrganizationUserType.User;
    public bool AccessSecretsManager { get; private init; }

    public static OrganizationUserSingleEmailInvite Create(string email,
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

        return new OrganizationUserSingleEmailInvite
        {
            Email = email,
            AccessibleCollections = accessibleCollections.ToArray(),
            Type = type,
            Permissions = permissions,
            AccessSecretsManager = accessSecretsManager
        };
    }
}
