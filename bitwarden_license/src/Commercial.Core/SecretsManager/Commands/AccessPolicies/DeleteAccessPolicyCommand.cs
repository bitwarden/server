using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class DeleteAccessPolicyCommand : IDeleteAccessPolicyCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public DeleteAccessPolicyCommand(
        IAccessPolicyRepository accessPolicyRepository,
        ICurrentContext currentContext,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
        _accessPolicyRepository = accessPolicyRepository;
        _currentContext = currentContext;
    }


    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var accessPolicy = await _accessPolicyRepository.GetByIdAsync(id);
        if (accessPolicy == null)
        {
            throw new NotFoundException();
        }

        switch (accessPolicy)
        {
            case UserProjectAccessPolicy ap:
                if (ap.GrantedProjectId == null)
                {
                    throw new BadRequestException();
                }

                var project = await _projectRepository.GetByIdAsync(ap.GrantedProjectId.Value);
                await CheckPermissionsAsync(project.OrganizationId, project.Id, userId);
                await _accessPolicyRepository.DeleteAsync(id);
                break;
            case GroupProjectAccessPolicy ap:
                if (ap.Group == null || ap.GrantedProjectId == null)
                {
                    throw new BadRequestException();
                }

                await CheckPermissionsAsync(ap.Group.OrganizationId, ap.GrantedProjectId.Value, userId);
                await _accessPolicyRepository.DeleteAsync(id);
                break;
            case ServiceAccountProjectAccessPolicy ap:
                if (ap.GrantedProjectId == null || ap.ServiceAccount == null)
                {
                    throw new BadRequestException();
                }

                await CheckPermissionsAsync(ap.ServiceAccount.OrganizationId, ap.GrantedProjectId.Value, userId);
                await _accessPolicyRepository.DeleteAsync(id);
                break;
            default:
                // FIX ME add in service account checks once service account permissions are done.
                throw new ArgumentException("Unsupported access policy type provided.", nameof(accessPolicy));
        }
    }


    private async Task CheckPermissionsAsync(Guid organizationId, Guid idToCheck, Guid userId)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        // FIXME once service account permission checks are merged check service account here.
        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(idToCheck, userId),
            _ => false,
        };

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException();
        }
    }
}
