namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IUpdateUserResetPasswordEnrollmentCommand
{
    Task UpdateAsync(Guid organizationId, Guid userId, string resetPasswordKey, Guid? callingUserId);
}

