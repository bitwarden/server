namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccountvNext;

/// <summary>
/// A strongly typed error containing a reason that an action failed.
/// This is used for business logic validation and other expected errors, not exceptions.
/// </summary>
public abstract record Error(string Message);
/// <summary>
/// An <see cref="Error"/> type that maps to a NotFoundResult at the api layer.
/// </summary>
/// <param name="Message"></param>
public abstract record NotFoundError(string Message) : Error(Message);

public record UserNotFoundError() : NotFoundError("Invalid user.");
public record UserNotClaimedError() : Error("Member is not claimed by the organization.");
public record InvalidUserStatusError() : Error("You cannot delete a member with Invited status.");
public record CannotDeleteYourselfError() : Error("You cannot delete yourself.");
public record CannotDeleteOwnersError() : Error("Only owners can delete other owners.");
public record SoleOwnerError() : Error("Cannot delete this user because it is the sole owner of at least one organization. Please delete these organizations or upgrade another user.");
public record SoleProviderError() : Error("Cannot delete this user because it is the sole owner of at least one provider. Please delete these providers or upgrade another user.");
public record CannotDeleteAdminsError() : Error("Custom users can not delete admins.");
