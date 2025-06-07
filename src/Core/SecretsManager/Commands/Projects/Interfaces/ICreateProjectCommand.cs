using Bit.Core.Identity;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Projects.Interfaces;

#nullable enable

public interface ICreateProjectCommand
{
    Task<Project> CreateAsync(Project project, Guid userId, IdentityClientType identityClientType);
}
