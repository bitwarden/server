using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public record CollectionNotFound : NotFoundError;
public record GroupNotFound : NotFoundError;

public record InviteUserFirst() : BadRequestError("Invite the user first.");
public record MustHaveConfirmedOwner() : BadRequestError("Organization must have at least one confirmed owner.");
public record ManageMutuallyExclusive() : BadRequestError("The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
public record CustomPermissionsNotEnabled() : BadRequestError("To enable custom permissions the organization must be on an Enterprise plan.");
public record CannotAssignDefaultCollection() : BadRequestError("Default collections cannot be assigned to a member.");
public record CannotAutoscaleSecretsManagerSeatsOnSelfHost() : BadRequestError("Cannot autoscale on a self-hosted instance.");
public record CouldNotIncreaseSeatsOfSecretManager(string Message) : BadRequestError(Message);

public abstract record EmailValidationError(string Message, string Type) : BadRequestError(Message), IValidationError
{
    public string PropertyName => "email";
}

public record MemberHasMasterPasswordError()
    : EmailValidationError("Cannot change the email of a member who has a master password.", "member_has_master_password");
public record MemberNotClaimedError()
    : EmailValidationError("Cannot change the email of a member who is not claimed by the organization.", "member_not_claimed");
public record NewEmailDomainNotClaimedError()
    : EmailValidationError("The new email address must be on a domain claimed by the organization.", "new_email_domain_not_claimed");
public record EmailAlreadyInUseError()
    : EmailValidationError("Email already in use.", "email_already_in_use");
public record EmailClaimedByAnotherOrganizationError()
    : EmailValidationError("This email address is claimed by an organization using Bitwarden.", "email_claimed_by_another_organization");
public record EmailChangeFailedError(string Message)
    : EmailValidationError(Message, "email_change_failed");
