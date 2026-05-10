using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class BulkChangeEmailForPasswordlessOrgUserCommand : IBulkChangeEmailForPasswordlessOrgUserCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IChangeEmailForPasswordlessOrgUserCommand _changeEmailForPasswordlessOrgUserCommand;

    public BulkChangeEmailForPasswordlessOrgUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IChangeEmailForPasswordlessOrgUserCommand changeEmailForPasswordlessOrgUserCommand)
    {
        _organizationUserRepository = organizationUserRepository;
        _changeEmailForPasswordlessOrgUserCommand = changeEmailForPasswordlessOrgUserCommand;
    }

    public async Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> BulkChangeOrganizationUserEmailAsync(
        Guid organizationId,
        IEnumerable<(Guid OrganizationUserId, string NewEmail)> requests)
    {
        var results = new List<(Guid OrganizationUserId, string ErrorMessage)>();

        foreach (var (organizationUserId, newEmail) in requests)
        {
            try
            {
                var organizationUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
                if (organizationUser == null || organizationUser.OrganizationId != organizationId)
                {
                    throw new NotFoundException();
                }

                await _changeEmailForPasswordlessOrgUserCommand.ChangeOrganizationUserEmailAsync(
                    organizationId, organizationUser, newEmail);

                results.Add((organizationUserId, string.Empty));
            }
            catch (Exception e)
            {
                results.Add((organizationUserId, e.Message));
            }
        }

        return results;
    }
}
