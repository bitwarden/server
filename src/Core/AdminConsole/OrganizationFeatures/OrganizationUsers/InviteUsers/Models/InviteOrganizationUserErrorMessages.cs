namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public static class InviteOrganizationUserErrorMessages
{
    public const string InvalidEmailErrorMessage = "The email address is not valid.";
    public const string InvalidCollectionConfigurationErrorMessage = "The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.";
}
