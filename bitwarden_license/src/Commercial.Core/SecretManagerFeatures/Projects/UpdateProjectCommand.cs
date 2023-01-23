using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Projects;

public class UpdateProjectCommand : IUpdateProjectCommand
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentContext _currentContext;

    public UpdateProjectCommand(IProjectRepository projectRepository, ICurrentContext currentContext)
    {
        _projectRepository = projectRepository;
        _currentContext = currentContext;
    }

    public async Task<Project> UpdateAsync(Project updatedProject, Guid userId)
    {
        var project = await _projectRepository.GetByIdAsync(updatedProject.Id);
        if (project == null)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(project.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(updatedProject.Id, userId),
            _ => false,
        };

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException();
        }

        project.Name = updatedProject.Name;
        project.RevisionDate = DateTime.UtcNow;

        await _projectRepository.ReplaceAsync(project);
        return project;
    }
}
