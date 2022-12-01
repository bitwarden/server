using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.OrganizationConnectionConfigs;

namespace Bit.Core.Entities;

public class SelfHostedOrganizationDetails : Organization
{
    public int OccupiedSeatCount { get; set; }
    public int CollectionCount { get; set; }
    public int GroupCount { get; set; }
    public IEnumerable<Policy> Policies { get; set; }
    public SsoConfig SsoConfig { get; set; }
    public IEnumerable<OrganizationConnection> ScimConnections { get; set; }

    public bool CanUseLicense(OrganizationLicense license, Organization existingOrganization, out string exception)
    {
        if (existingOrganization != null && existingOrganization.Id != Id)
        {
            exception = "License is already in use by another organization.";
            return false;
        }

        if (license.Seats.HasValue && OccupiedSeatCount > license.Seats.Value)
        {
            exception = $"Your organization currently has {OccupiedSeatCount} seats filled. " +
                $"Your new license only has ({license.Seats.Value}) seats. Remove some users.";
            return false;
        }

        if (license.MaxCollections.HasValue && CollectionCount > license.MaxCollections.Value)
        {
            exception = $"Your organization currently has {CollectionCount} collections. " +
                $"Your new license allows for a maximum of ({license.MaxCollections.Value}) collections. " +
                "Remove some collections.";
            return false;
        }

        if (!license.UseGroups && UseGroups && GroupCount > 1)
        {
            exception = $"Your organization currently has {GroupCount} groups. " +
                $"Your new license does not allow for the use of groups. Remove all groups.";
            return false;
        }

        var enabledPolicyCount = Policies.Count(p => p.Enabled);
        if (!license.UsePolicies && UsePolicies && enabledPolicyCount > 0)
        {
            exception = $"Your organization currently has {enabledPolicyCount} enabled " +
                $"policies. Your new license does not allow for the use of policies. Disable all policies.";
            return false;
        }

        if (!license.UseSso && UseSso && SsoConfig is { Enabled: true })
        {
            exception = $"Your organization currently has a SSO configuration. " +
                $"Your new license does not allow for the use of SSO. Disable your SSO configuration.";
            return false;
        }

        if (!license.UseKeyConnector && UseKeyConnector && SsoConfig != null && SsoConfig.GetData().KeyConnectorEnabled)
        {
            exception = $"Your organization currently has Key Connector enabled. " +
                $"Your new license does not allow for the use of Key Connector. Disable your Key Connector.";
            return false;
        }

        if (!license.UseScim && UseScim && ScimConnections != null &&
            ScimConnections.Any(c => c.GetConfig<ScimConfig>() is { Enabled: true }))
        {
            exception = "Your new plan does not allow the SCIM feature. " +
                "Disable your SCIM configuration.";
            return false;
        }

        if (!license.UseResetPassword && UseResetPassword &&
            Policies.Any(p => p.Type == PolicyType.ResetPassword && p.Enabled))
        {
            exception = "Your new license does not allow the Password Reset feature. "
                + "Disable your Password Reset policy.";
            return false;
        }

        exception = "";
        return true;
    }
}
public class SelfHostedOrganizationDetailsMapperProfile : Profile
{
    public SelfHostedOrganizationDetailsMapperProfile()
    {
        CreateMap<SelfHostedOrganizationDetails, Organization>();
    }
}
