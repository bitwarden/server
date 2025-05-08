using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.InviteOrganizationUserErrorMessages;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class OrganizationUserInviteCommandModel
{
    public string Email { get; private init; }
    public CollectionAccessSelection[] AssignedCollections { get; private init; }
    public OrganizationUserType Type { get; private init; }
    public Permissions Permissions { get; private init; }
    public string ExternalId { get; private init; }
    public bool AccessSecretsManager { get; private init; }
    public Guid[] Groups { get; private init; }

    public OrganizationUserInviteCommandModel(string email, string externalId) :
        this(
            email: email,
            assignedCollections: [],
            groups: [],
            type: OrganizationUserType.User,
            permissions: new Permissions(),
            externalId: externalId,
            false)
    {
    }

    public OrganizationUserInviteCommandModel(OrganizationUserInviteCommandModel invite, bool accessSecretsManager) :
        this(invite.Email,
            invite.AssignedCollections,
            invite.Groups,
            invite.Type,
            invite.Permissions,
            invite.ExternalId,
            accessSecretsManager)
    {

    }

    public OrganizationUserInviteCommandModel(string email,
        IEnumerable<CollectionAccessSelection> assignedCollections,
        IEnumerable<Guid> groups,
        OrganizationUserType type,
        Permissions permissions,
        string externalId,
        bool accessSecretsManager)
    {
        ValidateEmailAddress(email);

        var collections = assignedCollections?.ToArray() ?? [];

        if (collections.Any(x => x.IsValidCollectionAccessConfiguration()))
        {
            throw new BadRequestException(InvalidCollectionConfigurationErrorMessage);
        }

        Email = email;
        AssignedCollections = collections;
        Groups = groups.ToArray();
        Type = type;
        Permissions = permissions ?? new Permissions();
        ExternalId = externalId;
        AccessSecretsManager = accessSecretsManager;
    }

    private static void ValidateEmailAddress(string email)
    {
        if (!email.IsValidEmail())
        {
            throw new BadRequestException($"{email} {InvalidEmailErrorMessage}");
        }
    }
}
