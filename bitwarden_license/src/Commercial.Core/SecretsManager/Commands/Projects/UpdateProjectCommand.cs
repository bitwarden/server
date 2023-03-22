using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Projects;

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

        if (!_currentContext.AccessSecretsManager(project.OrganizationId))
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(project.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var access = await _projectRepository.AccessToProjectAsync(updatedProject.Id, userId, accessClient);
        if (!access.Write)
        {
            throw new NotFoundException();
        }

        project.Name = updatedProject.Name;
        project.RevisionDate = DateTime.UtcNow;

        await _projectRepository.ReplaceAsync(project);
        return project;
    }
}
