using Bit.Core.Entities;

namespace Bit.Commercial.Core.SecretsManager.Commands.Projects;
public interface IRequestSMAccessCommand
{
    Task<bool> SendRequestAccessToSM(Guid organizationId, User user, string emailContent);
}