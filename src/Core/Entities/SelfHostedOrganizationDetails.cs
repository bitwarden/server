using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.OrganizationConnectionConfigs;

namespace Bit.Core.Entities;

public class SelfHostedOrganizationDetails : Organization
{
    public IEnumerable<OrganizationUser> OrganizationUsers { get; set; }
    public int CollectionCount { get; set; }
    public int GroupCount { get; set; }
    public IEnumerable<Policy> Policies { get; set; }
    public SsoConfig SsoConfig { get; set; }
    public IEnumerable<OrganizationConnection> ScimConnections { get; set; }

    // TODO: should return an error string or null, rather than throw errors in a model
    public void CanUseLicense(OrganizationLicense license, Organization existingOrganization)
    {
        
        if (existingOrganization != null && existingOrganization.Id != Id)
        {
            throw new BadRequestException("License is already in use by another organization.");
        }

        var occupiedSeats = OrganizationUsers.Count(ou => ou.OccupiesOrganizationSeat);
        if (license.Seats.HasValue && occupiedSeats > license.Seats.Value)
        {
            throw new BadRequestException($"Your organization currently has {occupiedSeats} seats filled. " +
                $"Your new license only has ({license.Seats.Value}) seats. Remove some users.");
        }

        if (license.MaxCollections.HasValue && CollectionCount > license.MaxCollections.Value)
        {
            throw new BadRequestException($"Your organization currently has {CollectionCount} collections. " +
                $"Your new license allows for a maximum of ({license.MaxCollections.Value}) collections. " +
                "Remove some collections.");
        }

        if (!license.UseGroups && UseGroups && GroupCount > 1)
        {
            throw new BadRequestException($"Your organization currently has {GroupCount} groups. " +
                $"Your new license does not allow for the use of groups. Remove all groups.");
        }

        var enabledPolicyCount = Policies.Count(p => p.Enabled);
        if (!license.UsePolicies && UsePolicies && enabledPolicyCount > 0)
        {
            throw new BadRequestException($"Your organization currently has {enabledPolicyCount} enabled " +
                $"policies. Your new license does not allow for the use of policies. Disable all policies.");
        }

        if (!license.UseSso && UseSso && SsoConfig is { Enabled: true })
        {
            throw new BadRequestException($"Your organization currently has a SSO configuration. " +
                $"Your new license does not allow for the use of SSO. Disable your SSO configuration.");
        }

        if (!license.UseKeyConnector && UseKeyConnector && SsoConfig != null && SsoConfig.GetData().KeyConnectorEnabled)
        {
            throw new BadRequestException($"Your organization currently has Key Connector enabled. " +
                $"Your new license does not allow for the use of Key Connector. Disable your Key Connector.");
        }

        if (!license.UseScim && UseScim && ScimConnections != null && 
            ScimConnections.Any(c => c.GetConfig<ScimConfig>() is {Enabled: true}))
        {
            throw new BadRequestException("Your new plan does not allow the SCIM feature. " +
                "Disable your SCIM configuration.");
        }

        if (!license.UseResetPassword && UseResetPassword &&
            Policies.Any(p => p.Type == PolicyType.ResetPassword && p.Enabled))
        {
            throw new BadRequestException("Your new license does not allow the Password Reset feature. "
                + "Disable your Password Reset policy.");
        }
    }
}
public class SelfHostedOrganizationDetailsMapperProfile : Profile
{
    public SelfHostedOrganizationDetailsMapperProfile()
    {
        CreateMap<SelfHostedOrganizationDetails, Organization>().ReverseMap();
    }
}
