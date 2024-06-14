using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.SecretsManager.Commands.Requests.Interfaces;
using Bit.Core.Services;

namespace Bit.Commercial.Core.SecretsManager.Commands.Requests;

public class RequestSMAccessCommand : IRequestSMAccessCommand
{
    private readonly IMailService _mailService;

    public RequestSMAccessCommand(
        IMailService mailService)
    {
        _mailService = mailService;
    }

    public async Task SendRequestAccessToSM(Organization organization, ICollection<OrganizationUserUserDetails> orgUsers, User user, string emailContent)
    {
        var emailList = orgUsers.Where(o => o.Type <= OrganizationUserType.Admin || o.GetPermissions()?.ManageSso == true)
            .Select(a => a.Email).Distinct().ToList();

        await _mailService.SendRequestSMAccessToAdminEmailAsync(emailList, organization.Name, user.Name, emailContent);
    }
}
