using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Services.Implementations.UpgradeOrganizationPlan.Commands;

public static class UpdateOrganizationPropertiesCommand
{
    public static void UpdateOrganizationProperties(Organization organization, Plan passwordManagerPlan, OrganizationUpgrade upgrade
        , bool success, Plan secretManagerPlan)
    {
        organization.BusinessName = upgrade.BusinessName;
        organization.PlanType = passwordManagerPlan.Type;
        organization.Seats = (short)(passwordManagerPlan.BaseSeats + upgrade.AdditionalSeats);
        organization.SmSeats = (short)(secretManagerPlan.BaseSeats + upgrade.AdditionalSmSeats);
        if (secretManagerPlan.BaseServiceAccount != null)
            organization.SmServiceAccounts =
                (int)(secretManagerPlan.BaseServiceAccount + upgrade.AdditionalServiceAccount);
        organization.MaxCollections = passwordManagerPlan.MaxCollections;
        organization.UseGroups = passwordManagerPlan.HasGroups;
        organization.UseDirectory = passwordManagerPlan.HasDirectory;
        organization.UseEvents = passwordManagerPlan.HasEvents;
        organization.UseTotp = passwordManagerPlan.HasTotp;
        organization.Use2fa = passwordManagerPlan.Has2fa;
        organization.UseApi = passwordManagerPlan.HasApi;
        organization.SelfHost = passwordManagerPlan.HasSelfHost;
        organization.UsePolicies = passwordManagerPlan.HasPolicies;
        organization.MaxStorageGb = !passwordManagerPlan.BaseStorageGb.HasValue ?
            null : (short)(passwordManagerPlan.BaseStorageGb.Value + upgrade.AdditionalStorageGb);
        organization.UseGroups = passwordManagerPlan.HasGroups;
        organization.UseDirectory = passwordManagerPlan.HasDirectory;
        organization.UseEvents = passwordManagerPlan.HasEvents;
        organization.UseTotp = passwordManagerPlan.HasTotp;
        organization.Use2fa = passwordManagerPlan.Has2fa;
        organization.UseApi = passwordManagerPlan.HasApi;
        organization.UseSso = passwordManagerPlan.HasSso;
        organization.UseKeyConnector = passwordManagerPlan.HasKeyConnector;
        organization.UseScim = passwordManagerPlan.HasScim;
        organization.UseResetPassword = passwordManagerPlan.HasResetPassword;
        organization.SelfHost = passwordManagerPlan.HasSelfHost;
        organization.UsersGetPremium = passwordManagerPlan.UsersGetPremium || upgrade.PremiumAccessAddon;
        organization.UseCustomPermissions = passwordManagerPlan.HasCustomPermissions;
        organization.Plan = passwordManagerPlan.Name;
        organization.Enabled = success;
        organization.PublicKey = upgrade.PublicKey;
        organization.PrivateKey = upgrade.PrivateKey;
    }
}
