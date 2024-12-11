using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.SecretsManager.Commands.Requests.Interfaces;

public interface IRequestSMAccessCommand
{
    Task SendRequestAccessToSM(
        Organization organization,
        ICollection<OrganizationUserUserDetails> orgUsers,
        User user,
        string emailContent
    );
}
