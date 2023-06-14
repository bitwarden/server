using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;

public class ValidateUpgradeCommand : IValidateUpgradeCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;

    public ValidateUpgradeCommand(
        IOrganizationUserRepository organizationUserRepository
        , ICollectionRepository collectionRepository
        , IGroupRepository groupRepository
        , IPolicyRepository policyRepository
        , ISsoConfigRepository ssoConfigRepository
        , IOrganizationConnectionRepository organizationConnectionRepository)
    {
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _policyRepository = policyRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _organizationConnectionRepository = organizationConnectionRepository;
    }

    public void ValidatePlan(Plan newPlan, Plan existingPlan)
    {
        if (existingPlan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (newPlan == null)
        {
            throw new BadRequestException("Plan not found.");
        }

        if (newPlan.Disabled)
        {
            throw new BadRequestException("Plan not found.");
        }

        if (existingPlan.Type == newPlan.Type)
        {
            throw new BadRequestException("Organization is already on this plan.");
        }

        if (existingPlan.UpgradeSortOrder >= newPlan.UpgradeSortOrder)
        {
            throw new BadRequestException("You cannot upgrade to this plan.");
        }

        if (existingPlan.Type != PlanType.Free)
        {
            throw new BadRequestException("You can only upgrade from the free plan. Contact support.");
        }
    }

    public async Task ValidateSeatsAsync(Organization organization, Plan passwordManagerPlan, OrganizationUpgrade upgrade)
    {
        var newPlanSeats = (short)(passwordManagerPlan.BaseSeats +
                                   (passwordManagerPlan.HasAdditionalSeatsOption ? upgrade.AdditionalSeats : 0));
        if (!organization.Seats.HasValue || organization.Seats.Value > newPlanSeats)
        {
            var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSeats > newPlanSeats)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSeats} password manager seats filled. " +
                                              $"Your new plan only has ({newPlanSeats}) seats. Remove some users.");
            }
        }
    }

    public async Task ValidateSmSeatsAsync(Organization organization, Plan newPlan, OrganizationUpgrade upgrade)
    {
        var newPlanSeats = (short)(newPlan.BaseSeats + (newPlan.HasAdditionalSeatsOption ? upgrade.AdditionalSmSeats : 0));
        if (!organization.SmSeats.HasValue || organization.SmSeats.Value > newPlanSeats)
        {
            var occupiedSmSeats = await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSmSeats > newPlanSeats)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSmSeats} secrets manager seats filled. " +
                                              $"Your new plan only has ({newPlanSeats}) seats. Remove some users.");
            }
        }
    }

    public async Task ValidateServiceAccountAsync(Organization organization, Plan newPlan, OrganizationUpgrade upgrade)
    {
        if (newPlan.BaseServiceAccount != null)
        {
            var newPlanSeats = (short)(newPlan.BaseServiceAccount + (newPlan.HasAdditionalServiceAccountOption ? upgrade.AdditionalServiceAccount : 0));
            if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > newPlanSeats)
            {
                var occupiedServiceAccount = await _organizationUserRepository.GetOccupiedServiceAccountCountByOrganizationIdAsync(organization.Id);
                if (occupiedServiceAccount > newPlanSeats)
                {
                    throw new BadRequestException($"Your organization currently has {occupiedServiceAccount} service account seats filled. " +
                                                  $"Your new plan only has ({newPlanSeats}) service accounts. Remove some service accounts.");
                }
            }
        }
    }

    public async Task ValidateCollectionsAsync(Organization organization, Plan newPlan)
    {
        if (newPlan.MaxCollections.HasValue && (!organization.MaxCollections.HasValue ||
                                                organization.MaxCollections.Value > newPlan.MaxCollections.Value))
        {
            var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
            if (collectionCount > newPlan.MaxCollections.Value)
            {
                throw new BadRequestException($"Your organization currently has {collectionCount} collections. " +
                                              $"Your new plan allows for a maximum of ({newPlan.MaxCollections.Value}) collections. " +
                                              "Remove some collections.");
            }
        }
    }

    public async Task ValidateGroupsAsync(Organization organization, Plan newPlan)
    {
        if (!newPlan.HasGroups && organization.UseGroups)
        {
            var groups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
            if (groups.Any())
            {
                throw new BadRequestException($"Your new plan does not allow the groups feature. " +
                                              $"Remove your groups.");
            }
        }
    }

    public async Task ValidatePoliciesAsync(Organization organization, Plan newPlan)
    {
        if (!newPlan.HasPolicies && organization.UsePolicies)
        {
            var policies = await _policyRepository.GetManyByOrganizationIdAsync(organization.Id);
            if (policies.Any(p => p.Enabled))
            {
                throw new BadRequestException($"Your new plan does not allow the policies feature. " +
                                              $"Disable your policies.");
            }
        }
    }

    public async Task ValidateSsoAsync(Organization organization, Plan newPlan)
    {
        if (!newPlan.HasSso && organization.UseSso)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig != null && ssoConfig.Enabled)
            {
                throw new BadRequestException($"Your new plan does not allow the SSO feature. " +
                                              $"Disable your SSO configuration.");
            }
        }
    }

    public async Task ValidateKeyConnectorAsync(Organization organization, Plan newPlan)
    {
        if (!newPlan.HasKeyConnector && organization.UseKeyConnector)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig != null && ssoConfig.GetData().MemberDecryptionType == MemberDecryptionType.KeyConnector)
            {
                throw new BadRequestException($"Your new plan does not allow the Key Connector feature. " +
                                              $"Disable your Key Connector configuration.");
            }
        }
    }

    public async Task ValidateResetPasswordAsync(Organization organization, Plan newPlan)
    {
        if (!newPlan.HasResetPassword && organization.UseResetPassword)
        {
            var resetPasswordPolicy = await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
            if (resetPasswordPolicy != null && resetPasswordPolicy.Enabled)
            {
                throw new BadRequestException("Your new plan does not allow the Password Reset feature. " +
                                              "Disable your Password Reset policy.");
            }
        }
    }

    public async Task ValidateScimAsync(Organization organization, Plan newPlan)
    {
        if (!newPlan.HasScim && organization.UseScim)
        {
            var scimConnections = await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(organization.Id,
                OrganizationConnectionType.Scim);
            if (scimConnections != null && scimConnections.Any(c => c.GetConfig<ScimConfig>()?.Enabled == true))
            {
                throw new BadRequestException("Your new plan does not allow the SCIM feature. " +
                                              "Disable your SCIM configuration.");
            }
        }
    }

    public async Task ValidateCustomPermissionsAsync(Organization organization, Plan newPlan)
    {
        if (!newPlan.HasCustomPermissions && organization.UseCustomPermissions)
        {
            var organizationCustomUsers =
                await _organizationUserRepository.GetManyByOrganizationAsync(organization.Id,
                    OrganizationUserType.Custom);
            if (organizationCustomUsers.Any())
            {
                throw new BadRequestException("Your new plan does not allow the Custom Permissions feature. " +
                                              "Disable your Custom Permissions configuration.");
            }
        }
    }
}
