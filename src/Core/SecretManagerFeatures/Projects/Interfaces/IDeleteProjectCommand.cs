using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.Projects.Interfaces;

public interface IDeleteProjectCommand
{
    Task<List<Tuple<Project, string>>> DeleteProjects(List<Guid> ids, Guid userId);
}

