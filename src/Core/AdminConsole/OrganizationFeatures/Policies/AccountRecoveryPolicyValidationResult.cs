namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// The result of <see cref="IJoinOrganizationPolicyValidationService.ValidateAccountRecoveryPolicyAsync(Guid, Bit.Core.Entities.User, Bit.Core.Enums.OrganizationUserType, string?)"/>.
/// </summary>
/// <param name="ResetPasswordKeyMissing">
/// True if account recovery auto-enroll is on and the reset password key is missing or invalid.
/// </param>
/// <param name="AutoEnrollEnabled">If true, the caller must enroll the user in account recovery once they join.</param>
public sealed record AccountRecoveryPolicyValidationResult(bool ResetPasswordKeyMissing, bool AutoEnrollEnabled);
