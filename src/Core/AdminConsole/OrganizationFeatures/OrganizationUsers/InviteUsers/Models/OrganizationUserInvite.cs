using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.InviteOrganizationUserErrorMessages;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.InviteOrganizationUserFunctions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class OrganizationUserInvite
{
    public string[] Emails { get; private init; } = [];
    public CollectionAccessSelection[] AccessibleCollections { get; private init; } = [];
    public OrganizationUserType Type { get; private init; } = OrganizationUserType.User;
    public Permissions Permissions { get; private init; } = new();
    public string ExternalId { get; private init; } = string.Empty;
    public bool AccessSecretsManager { get; private init; }
    public Guid[] Groups { get; private init; } = [];

    public static OrganizationUserInvite Create(string[] emails,
        IEnumerable<CollectionAccessSelection> accessibleCollections,
        OrganizationUserType type,
        Permissions permissions,
        string externalId,
        bool accessSecretsManager)
    {
        ValidateEmailAddresses(emails);

        if (accessibleCollections?.Any(ValidateCollectionConfiguration) ?? false)
        {
            throw new BadRequestException(InvalidCollectionConfigurationErrorMessage);
        }

        return new OrganizationUserInvite
        {
            Emails = emails,
            AccessibleCollections = accessibleCollections.ToArray(),
            Type = type,
            Permissions = permissions,
            ExternalId = externalId,
            AccessSecretsManager = accessSecretsManager
        };
    }

    public static OrganizationUserInvite Create(OrganizationUserSingleEmailInvite invite) =>
        Create([invite.Email],
            invite.AccessibleCollections,
            invite.Type,
            invite.Permissions,
            invite.ExternalId,
            invite.AccessSecretsManager);

    private static void ValidateEmailAddresses(string[] emails)
    {
        foreach (var email in emails)
        {
            if (!email.IsValidEmail())
            {
                throw new BadRequestException($"{email} {InvalidEmailErrorMessage}");
            }
        }
    }
}
