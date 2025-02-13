using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;

public class OrganizationUserSingleEmailInvite
{
    public const string InvalidEmailErrorMessage = "The email address is not valid.";
    public const string InvalidCollectionConfigurationErrorMessage = "The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.";

    public string Email { get; private init; } = string.Empty;
    public Guid[] AccessibleCollections { get; private init; } = [];
    public string ExternalId { get; private init; } = string.Empty;
    public Permissions Permissions { get; private init; } = new();
    public OrganizationUserType Type { get; private init; } = OrganizationUserType.User; // Need to set
    public bool AccessSecretsManager { get; private init; }

    public static OrganizationUserSingleEmailInvite Create(string email,
        IEnumerable<CollectionAccessSelection> accessibleCollections,
        string externalId,
        OrganizationUserType type,
        Permissions permissions,
        bool accessSecretsManager)
    {
        if (!email.IsValidEmail())
        {
            throw new BadRequestException(InvalidEmailErrorMessage);
        }

        if (accessibleCollections?.Any(Functions.ValidateCollectionConfiguration) ?? false)
        {
            throw new BadRequestException(InvalidCollectionConfigurationErrorMessage);
        }

        return new OrganizationUserSingleEmailInvite
        {
            Email = email,
            AccessibleCollections = accessibleCollections.Select(x => x.Id).ToArray(),
            ExternalId = externalId,
            Type = type,
            Permissions = permissions,
            AccessSecretsManager = accessSecretsManager
        };
    }
}
