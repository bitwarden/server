using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Projects.Interfaces;

public interface IDeleteProjectCommand
{
    Task DeleteProjects(ICollection<Project> projects);
}

