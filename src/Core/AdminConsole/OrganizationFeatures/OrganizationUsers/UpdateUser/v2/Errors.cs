using Bit.Core.AdminConsole.Utilities.v2;

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
