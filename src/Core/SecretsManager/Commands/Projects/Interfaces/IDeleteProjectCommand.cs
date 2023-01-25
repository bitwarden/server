using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Projects.Interfaces;

public interface IDeleteProjectCommand
{
    Task<List<Tuple<Project, string>>> DeleteProjects(List<Guid> ids, Guid userId);
}

