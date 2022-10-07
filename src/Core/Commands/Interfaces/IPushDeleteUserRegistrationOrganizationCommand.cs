namespace Bit.Core.Commands.Interfaces;

public interface IPushDeleteUserRegistrationOrganizationCommand
{
    Task DeleteAndPushUserRegistrationAsync(Guid organizationId, Guid userId);
}
