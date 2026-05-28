using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Organizations.Extensions;

public static class OrganizationPlanExtensions
{
    /// <summary>
    /// Applies the structural plan-shape of <paramref name="newPlan"/> to
    /// <paramref name="organization"/>. Pure mutation — no IO, no persistence, no
    /// validation.
    ///
    /// Structural plan-shape fields written by this helper:
    ///   PlanType, Plan, all Use* capability flags, UsersGetPremium,
    ///   MaxCollections, UsePasswordManager.
    ///
    /// Customer-purchase fields intentionally NOT touched (owned by callers):
    ///   Seats, MaxStorageGb, SmSeats, SmServiceAccounts, MaxAutoscale*,
    ///   UseSecretsManager, BusinessName, Enabled.
    /// </summary>
    public static void ChangePlan(this Organization organization, Plan newPlan)
    {
        organization.PlanType = newPlan.Type;
        organization.Plan = newPlan.Name;
        organization.UseGroups = newPlan.HasGroups;
        organization.UseDirectory = newPlan.HasDirectory;
        organization.UseEvents = newPlan.HasEvents;
        organization.UseTotp = newPlan.HasTotp;
        organization.Use2fa = newPlan.Has2fa;
        organization.UseApi = newPlan.HasApi;
        organization.SelfHost = newPlan.HasSelfHost;
        organization.UsePolicies = newPlan.HasPolicies;
        organization.UseMyItems = newPlan.HasMyItems;
        organization.UseInviteLinks = newPlan.HasInviteLinks;
        organization.UseSso = newPlan.HasSso;
        organization.UseOrganizationDomains = newPlan.HasOrganizationDomains;
        organization.UseKeyConnector = newPlan.HasKeyConnector ? organization.UseKeyConnector : false;
        organization.UseScim = newPlan.HasScim;
        organization.UseResetPassword = newPlan.HasResetPassword;
        organization.UseCustomPermissions = newPlan.HasCustomPermissions;
        organization.UsersGetPremium = newPlan.UsersGetPremium;
        organization.UsePasswordManager = true;
        organization.MaxCollections = newPlan.PasswordManager.MaxCollections;
    }
}
