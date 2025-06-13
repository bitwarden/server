using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Projects.Interfaces;

#nullable enable

public interface IDeleteProjectCommand
{
    Task DeleteProjects(IEnumerable<Project> projects);
}

