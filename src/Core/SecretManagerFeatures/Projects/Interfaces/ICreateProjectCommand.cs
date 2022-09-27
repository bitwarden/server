using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.Projects.Interfaces;

public interface ICreateProjectCommand
{
    Task<Project> CreateAsync(Project project);
}
