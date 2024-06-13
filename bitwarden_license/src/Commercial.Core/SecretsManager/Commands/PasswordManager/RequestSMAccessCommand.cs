using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Commercial.Core.SecretsManager.Commands.PasswordManager;

public class RequestSMAccessCommand : IRequestSMAccessCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMailService _mailService;

    public RequestSMAccessCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IMailService mailService)
    {
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task SendRequestAccessToSM(Guid organizationId, User user, string emailContent)
    {
        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var emailList = orgUsers.Where(o => o.Type <= OrganizationUserType.Admin || o.GetPermissions()?.ManageSso == true)
            .Select(a => a.Email).Distinct().ToList();

        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        await _mailService.SendRequestSMAccessToAdminEmailAsync(emailList, organization.Name, user.Name, emailContent);
    }
}
