using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public interface IInviteOrganizationUsersCommand
{
    Task InviteOrganizationUserAsync(InviteOrganizationUserRequest request);
    Task InviteOrganizationUsersAsync(InviteOrganizationUsersRequest request);
}

public class InviteOrganizationUsersCommand : IInviteOrganizationUsersCommand
{
    public Task InviteOrganizationUserAsync(InviteOrganizationUserRequest request) => throw new NotImplementedException();

    public Task InviteOrganizationUsersAsync(InviteOrganizationUsersRequest request) => throw new NotImplementedException();
}

public record InviteOrganizationUsersRequest();

public record InviteOrganizationUserRequest(
    OrganizationUserSingleInvite Invite,
    OrganizationDto Organization,
    Guid performedBy,
    DateTimeOffset performedAt);

public class OrganizationUserSingleInvite
{
    public const string InvalidEmailErrorMessage = "The email address is not valid.";
    public const string InvalidCollecitonConfigurationErrorMessage = "The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.";

    public string Email { get; private init; } = string.Empty;
    public Guid[] AccessibleCollections { get; private init; } = [];

    public static OrganizationUserSingleInvite Create(string email, IEnumerable<CollectionAccessSelection> accessibleCollections)
    {
        if (!email.IsValidEmail())
        {
            throw new BadRequestException(InvalidEmailErrorMessage);
        }

        if (accessibleCollections?.Any(ValidateCollectionConfiguration) ?? false)
        {
            throw new BadRequestException(InvalidCollecitonConfigurationErrorMessage);
        }

        return new OrganizationUserSingleInvite
        {
            Email = email,
            AccessibleCollections = accessibleCollections.Select(x => x.Id).ToArray()
        };
    }

    private static Func<CollectionAccessSelection, bool> ValidateCollectionConfiguration => collectionAccessSelection =>
            collectionAccessSelection.Manage && (collectionAccessSelection.ReadOnly || collectionAccessSelection.HidePasswords);
}
