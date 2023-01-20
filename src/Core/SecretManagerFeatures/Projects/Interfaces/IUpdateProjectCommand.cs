using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.Projects.Interfaces;

public interface IUpdateProjectCommand
{
    Task<Project> UpdateAsync(Project updatedProject, Guid userId);
}
