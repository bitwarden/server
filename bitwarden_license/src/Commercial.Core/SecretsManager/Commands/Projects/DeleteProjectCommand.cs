using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Projects;

public class DeleteProjectCommand : IDeleteProjectCommand
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;

    public DeleteProjectCommand(IProjectRepository projectRepository, ICurrentContext currentContext, ISecretRepository secretRepository)
    {
        _projectRepository = projectRepository;
        _currentContext = currentContext;
        _secretRepository = secretRepository;
    }

    public async Task<List<Tuple<Project, string>>> DeleteProjects(List<Guid> ids, Guid userId)
    {
        if (ids.Any() != true || userId == new Guid())
        {
            throw new ArgumentNullException();
        }

        var projects = (await _projectRepository.GetManyWithSecretsByIds(ids))?.ToList();

        if (projects?.Any() != true || projects.Count != ids.Count)
        {
            throw new NotFoundException();
        }

        // Ensure all projects belongs to the same organization
        var organizationId = projects.First().OrganizationId;
        if (projects.Any(p => p.OrganizationId != organizationId))
        {
            throw new BadRequestException();
        }

        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var results = new List<Tuple<Project, string>>(projects.Count);
        var deleteIds = new List<Guid>();

        foreach (var project in projects)
        {
            var access = await _projectRepository.AccessToProjectAsync(project.Id, userId, accessClient);
            if (!access.Write)
            {
                results.Add(new Tuple<Project, string>(project, "access denied"));
            }
            else
            {
                results.Add(new Tuple<Project, string>(project, ""));
                deleteIds.Add(project.Id);
            }
        }

        if (deleteIds.Count > 0)
        {
            var secretIds = results.SelectMany(projTuple => projTuple.Item1?.Secrets?.Select(s => s.Id) ?? Array.Empty<Guid>()).ToList();

            if (secretIds.Count > 0)
            {
                await _secretRepository.UpdateRevisionDates(secretIds);
            }

            await _projectRepository.DeleteManyByIdAsync(deleteIds);
        }

        return results;
    }
}
