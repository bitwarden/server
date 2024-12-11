using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Projects.Interfaces;

public interface IDeleteProjectCommand
{
    Task DeleteProjects(IEnumerable<Project> projects);
}
