namespace Bit.Core.SecretManagerFeatures.Projects.Interfaces;

public interface IDeleteProjectCommand
{
    Task<List<Tuple<Guid, string>>> DeleteProjects(List<Guid> ids);
}

