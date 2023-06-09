using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Models.StaticStore;
using Bit.Core.Exceptions;
using Bit.Core.Auth.Repositories;
using Bit.Core.Enums;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;

namespace Bit.Core.Services.UpgradeOrganizationPlan.Commands;

public class ValidateUpgradeCommand
{
    public static void ValidatePlanAsync(Plan newPlan,Plan existingPlan)
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
    
    public static async Task ValidateSeatsAsync(Organization organization, Plan newPlan, OrganizationUpgrade upgrade,
        IOrganizationUserRepository organizationUserRepository)
    {
        var newPlanSeats = (short)(newPlan.BaseSeats +
                                   (newPlan.HasAdditionalSeatsOption ? upgrade.AdditionalSeats : 0));
        if (!organization.Seats.HasValue || organization.Seats.Value > newPlanSeats)
        {
            var occupiedSeats = await organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSeats > newPlanSeats)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSeats} password manager seats filled. " +
                                              $"Your new plan only has ({newPlanSeats}) seats. Remove some users.");
            }
        }
    }
    
    public static async Task ValidateSmSeatsAsync(Organization organization, Plan newPlan, OrganizationUpgrade upgrade,
        IOrganizationUserRepository organizationUserRepository)
    {
        var newPlanSeats = (short)(newPlan.BaseSeats + (newPlan.HasAdditionalSeatsOption ? upgrade.AdditionalSmSeats : 0));
        if (!organization.SmSeats.HasValue || organization.SmSeats.Value > newPlanSeats)
        {
            var occupiedSmSeats = await organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSmSeats > newPlanSeats)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSmSeats} secrets manager seats filled. " +
                                              $"Your new plan only has ({newPlanSeats}) seats. Remove some users.");
            }
        }
    }
    
    public static async Task ValidateServiceAccountAsync(Organization organization, Plan newPlan, OrganizationUpgrade upgrade,
        IOrganizationUserRepository organizationUserRepository)
    {
        var newPlanSeats = (short)(newPlan.BaseServiceAccount + (newPlan.HasAdditionalServiceAccountOption ? upgrade.AdditionalServiceAccount : 0));
        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > newPlanSeats)
        {
            var occupiedServiceAccount = await organizationUserRepository.GetOccupiedServiceAccountCountByOrganizationIdAsync(organization.Id);
            if (occupiedServiceAccount > newPlanSeats)
            {
                throw new BadRequestException($"Your organization currently has {occupiedServiceAccount} service account seats filled. " +
                                              $"Your new plan only has ({newPlanSeats}) service accounts. Remove some service accounts.");
            }
        }
    }
    
    public static async Task ValidateCollectionsAsync(Organization organization, Plan newPlan,
        ICollectionRepository collectionRepository)
    {
        if (newPlan.MaxCollections.HasValue && (!organization.MaxCollections.HasValue ||
                                                organization.MaxCollections.Value > newPlan.MaxCollections.Value))
        {
            var collectionCount = await collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
            if (collectionCount > newPlan.MaxCollections.Value)
            {
                throw new BadRequestException($"Your organization currently has {collectionCount} collections. " +
                                              $"Your new plan allows for a maximum of ({newPlan.MaxCollections.Value}) collections. " +
                                              "Remove some collections.");
            }
        }
    }
    
    public static async Task ValidateGroupsAsync(Organization organization, Plan newPlan, IGroupRepository groupRepository)
    {
        if (!newPlan.HasGroups && organization.UseGroups)
        {
            var groups = await groupRepository.GetManyByOrganizationIdAsync(organization.Id);
            if (groups.Any())
            {
                throw new BadRequestException($"Your new plan does not allow the groups feature. " +
                                              $"Remove your groups.");
            }
        }
    }
    
    public static async Task ValidatePoliciesAsync(Organization organization, Plan newPlan, IPolicyRepository policyRepository)
    {
        if (!newPlan.HasPolicies && organization.UsePolicies)
        {
            var policies = await policyRepository.GetManyByOrganizationIdAsync(organization.Id);
            if (policies.Any(p => p.Enabled))
            {
                throw new BadRequestException($"Your new plan does not allow the policies feature. " +
                                              $"Disable your policies.");
            }
        }
    }
    
    public static async Task ValidateSsoAsync(Organization organization, Plan newPlan, ISsoConfigRepository ssoConfigRepository)
    {
        if (!newPlan.HasSso && organization.UseSso)
        {
            var ssoConfig = await ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig != null && ssoConfig.Enabled)
            {
                throw new BadRequestException($"Your new plan does not allow the SSO feature. " +
                                              $"Disable your SSO configuration.");
            }
        }
    }
    
    public static async Task ValidateKeyConnectorAsync(Organization organization, Plan newPlan, ISsoConfigRepository ssoConfigRepository)
    {
        if (!newPlan.HasKeyConnector && organization.UseKeyConnector)
        {
            var ssoConfig = await ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig != null && ssoConfig.GetData().MemberDecryptionType == MemberDecryptionType.KeyConnector)
            {
                throw new BadRequestException($"Your new plan does not allow the Key Connector feature. " +
                                              $"Disable your Key Connector configuration.");
            }
        }
    }
    
    public static async Task ValidateResetPasswordAsync(Organization organization, Plan newPlan, IPolicyRepository policyRepository)
    {
        if (!newPlan.HasResetPassword && organization.UseResetPassword)
        {
            var resetPasswordPolicy = await policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
            if (resetPasswordPolicy != null && resetPasswordPolicy.Enabled)
            {
                throw new BadRequestException("Your new plan does not allow the Password Reset feature. " +
                                              "Disable your Password Reset policy.");
            }
        }
    }
    
    public static async Task ValidateScimAsync(Organization organization, Plan newPlan, IOrganizationConnectionRepository organizationConnectionRepository)
    {
        if (!newPlan.HasScim && organization.UseScim)
        {
            var scimConnections = await organizationConnectionRepository.GetByOrganizationIdTypeAsync(organization.Id,
                OrganizationConnectionType.Scim);
            if (scimConnections != null && scimConnections.Any(c => c.GetConfig<ScimConfig>()?.Enabled == true))
            {
                throw new BadRequestException("Your new plan does not allow the SCIM feature. " +
                                              "Disable your SCIM configuration.");
            }
        }
    }
    
    public static async Task ValidateCustomPermissionsAsync(Organization organization, Plan newPlan,
        IOrganizationUserRepository organizationUserRepository)
    {
        if (!newPlan.HasCustomPermissions && organization.UseCustomPermissions)
        {
            var organizationCustomUsers =
                await organizationUserRepository.GetManyByOrganizationAsync(organization.Id,
                    OrganizationUserType.Custom);
            if (organizationCustomUsers.Any())
            {
                throw new BadRequestException("Your new plan does not allow the Custom Permissions feature. " +
                                              "Disable your Custom Permissions configuration.");
            }
        }
    }
}
