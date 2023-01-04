using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.Projects;

public class DeleteProjectCommand : IDeleteProjectCommand
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentContext _currentContext;

    public DeleteProjectCommand(IProjectRepository projectRepository, ICurrentContext currentContext)
    {
        _projectRepository = projectRepository;
        _currentContext = currentContext;
    }

    public async Task<List<Tuple<Project, string>>> DeleteProjects(List<Guid> ids, Guid userId)
    {
        if(ids.Any() != true || userId == null){
            throw new ArgumentNullException();
        }

        var projects = (await _projectRepository.GetManyByIds(ids))?.ToList();

        if (projects.Any() != true)
        {
            throw new NotFoundException();
        }

        // Ensure all projects belongs to the same organization
        var organizationId = projects.First().OrganizationId;
        if (projects.Any(p => p.OrganizationId != organizationId))
        {
            throw new UnauthorizedAccessException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var results = new List<Tuple<Project, String>>(projects.Count);
        var deleteIds = new List<Guid>();

        foreach (var project in projects)
        {
            var hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(project.Id, userId),
                _ => false,
            };

            if (!hasAccess)
            {
                results.Add(new Tuple<Project, string>(project, "access denied"));
            }
            else
            {
                results.Add(new Tuple<Project, string>(project, ""));
                deleteIds.Add(project.Id);
            }
        }

        await _projectRepository.DeleteManyByIdAsync(deleteIds);
        return results;
    }
}

