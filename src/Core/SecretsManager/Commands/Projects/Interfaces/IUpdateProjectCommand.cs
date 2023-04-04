using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Projects.Interfaces;

public interface IUpdateProjectCommand
{
    Task<Project> UpdateAsync(Project updatedProject);
}
