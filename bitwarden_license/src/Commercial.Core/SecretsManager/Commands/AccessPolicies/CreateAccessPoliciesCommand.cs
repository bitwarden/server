using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class CreateAccessPoliciesCommand : ICreateAccessPoliciesCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public CreateAccessPoliciesCommand(
        IAccessPolicyRepository accessPolicyRepository,
        ICurrentContext currentContext,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<IEnumerable<BaseAccessPolicy>> CreateForProjectAsync(Guid projectId,
        List<BaseAccessPolicy> accessPolicies, Guid userId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null || !_currentContext.AccessSecretsManager(project.OrganizationId))
        {
            throw new NotFoundException();
        }

        await CheckPermissionAsync(project.OrganizationId, userId, projectId);
        CheckForDistinctAccessPolicies(accessPolicies);
        await CheckAccessPoliciesDoNotExistAsync(accessPolicies);

        await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        return await _accessPolicyRepository.GetManyByGrantedProjectIdAsync(projectId);
    }

    public async Task<IEnumerable<BaseAccessPolicy>> CreateForServiceAccountAsync(Guid serviceAccountId,
        List<BaseAccessPolicy> accessPolicies, Guid userId)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(serviceAccountId);
        if (serviceAccount == null || !_currentContext.AccessSecretsManager(serviceAccount.OrganizationId))
        {
            throw new NotFoundException();
        }

        await CheckPermissionAsync(serviceAccount.OrganizationId, userId, serviceAccountIdToCheck: serviceAccountId);
        CheckForDistinctAccessPolicies(accessPolicies);
        await CheckAccessPoliciesDoNotExistAsync(accessPolicies);

        await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        return await _accessPolicyRepository.GetManyByGrantedServiceAccountIdAsync(serviceAccountId);
    }

    private static void CheckForDistinctAccessPolicies(IReadOnlyCollection<BaseAccessPolicy> accessPolicies)
    {
        var distinctAccessPolicies = accessPolicies.DistinctBy(baseAccessPolicy =>
        {
            return baseAccessPolicy switch
            {
                UserProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId, ap.GrantedProjectId),
                GroupProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedProjectId),
                ServiceAccountProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.ServiceAccountId,
                    ap.GrantedProjectId),
                UserServiceAccountAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId,
                    ap.GrantedServiceAccountId),
                GroupServiceAccountAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedServiceAccountId),
                _ => throw new ArgumentException("Unsupported access policy type provided.", nameof(baseAccessPolicy)),
            };
        }).ToList();

        if (accessPolicies.Count != distinctAccessPolicies.Count)
        {
            throw new BadRequestException("Resources must be unique");
        }
    }

    private async Task CheckAccessPoliciesDoNotExistAsync(List<BaseAccessPolicy> accessPolicies)
    {
        foreach (var accessPolicy in accessPolicies)
        {
            if (await _accessPolicyRepository.AccessPolicyExists(accessPolicy))
            {
                throw new BadRequestException("Resource already exists");
            }
        }
    }

    private async Task CheckPermissionAsync(Guid organizationId,
        Guid userId,
        Guid? projectIdToCheck = null,
        Guid? serviceAccountIdToCheck = null)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        bool hasAccess;
        switch (accessClient)
        {
            case AccessClientType.NoAccessCheck:
                hasAccess = true;
                break;
            case AccessClientType.User:
                if (projectIdToCheck != null)
                {
                    hasAccess = await _projectRepository.UserHasWriteAccessToProject(projectIdToCheck.Value, userId);
                }
                else if (serviceAccountIdToCheck != null)
                {
                    hasAccess = await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(
                        serviceAccountIdToCheck.Value,
                        userId);
                }
                else
                {
                    hasAccess = false;
                }
                break;
            default:
                hasAccess = false;
                break;
        }

        if (!hasAccess)
        {
            throw new NotFoundException();
        }
    }
}
