using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Services.UpgradeOrganizationPlan.Commands;

public static class UpdateOrganizationPropertiesCommand
{
    public static void UpdateOrganizationProperties(Organization organization, Plan newPlan, OrganizationUpgrade upgrade, bool success)
    {
        organization.BusinessName = upgrade.BusinessName;
        organization.PlanType = newPlan.Type;
        organization.Seats = (short)(newPlan.BaseSeats + upgrade.AdditionalSeats);
        organization.MaxCollections = newPlan.MaxCollections;
        organization.UseGroups = newPlan.HasGroups;
        organization.UseDirectory = newPlan.HasDirectory;
        organization.UseEvents = newPlan.HasEvents;
        organization.UseTotp = newPlan.HasTotp;
        organization.Use2fa = newPlan.Has2fa;
        organization.UseApi = newPlan.HasApi;
        organization.SelfHost = newPlan.HasSelfHost;
        organization.UsePolicies = newPlan.HasPolicies;
        organization.MaxStorageGb = !newPlan.BaseStorageGb.HasValue ?
            (short?)null : (short)(newPlan.BaseStorageGb.Value + upgrade.AdditionalStorageGb);
        organization.UseGroups = newPlan.HasGroups;
        organization.UseDirectory = newPlan.HasDirectory;
        organization.UseEvents = newPlan.HasEvents;
        organization.UseTotp = newPlan.HasTotp;
        organization.Use2fa = newPlan.Has2fa;
        organization.UseApi = newPlan.HasApi;
        organization.UseSso = newPlan.HasSso;
        organization.UseKeyConnector = newPlan.HasKeyConnector;
        organization.UseScim = newPlan.HasScim;
        organization.UseResetPassword = newPlan.HasResetPassword;
        organization.SelfHost = newPlan.HasSelfHost;
        organization.UsersGetPremium = newPlan.UsersGetPremium || upgrade.PremiumAccessAddon;
        organization.UseCustomPermissions = newPlan.HasCustomPermissions;
        organization.Plan = newPlan.Name;
        organization.Enabled = success;
        organization.PublicKey = upgrade.PublicKey;
        organization.PrivateKey = upgrade.PrivateKey;
    }
}
