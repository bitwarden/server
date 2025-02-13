using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public record OrganizationUserInvite
{
    public const string InvalidEmailErrorMessage = "is not a valid email address.";
    public const string InvalidCollectionConfigurationErrorMessage = "The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.";

    public string[] Emails { get; private init; } = [];
    public Guid[] AccessibleCollections { get; private init; } = [];
    public OrganizationUserType Type { get; private init; } = OrganizationUserType.User; // Need to set

    public Permissions Permissions { get; private init; } = new(); // Need to set
    public string ExternalId { get; private init; } = string.Empty;
    public bool AccessSecretsManager { get; private init; }

    public static OrganizationUserInvite Create(string[] emails,
        IEnumerable<CollectionAccessSelection> accessibleCollections,
        OrganizationUserType type,
        Permissions permissions,
        string externalId,
        bool accessSecretsManager)
    {
        if (accessibleCollections?.Any(Functions.ValidateCollectionConfiguration) ?? false)
        {
            throw new BadRequestException(InvalidCollectionConfigurationErrorMessage);
        }

        return Create(emails, accessibleCollections?.Select(x => x.Id), type, permissions, externalId, accessSecretsManager);
    }

    private static OrganizationUserInvite Create(string[] emails, IEnumerable<Guid> accessibleCollections, OrganizationUserType type, Permissions permissions, string externalId, bool accessSecretsManager)
    {
        ValidateEmailAddresses(emails);

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
        Create([invite.Email], invite.AccessibleCollections, invite.Type, invite.Permissions, invite.ExternalId, invite.AccessSecretsManager);

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
