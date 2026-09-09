namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUserResetPasswordEnrollment;

public interface IUpdateUserResetPasswordEnrollmentCommand
{
    /// <summary>
    /// Enrolls or withdraws an organization user from account recovery. Pass a non-null
    /// <paramref name="resetPasswordKey"/> to enroll, or null to withdraw.
    /// </summary>
    Task UpdateUserResetPasswordEnrollmentAsync(Guid organizationId, Guid userId, string? resetPasswordKey, Guid? callingUserId);
}
