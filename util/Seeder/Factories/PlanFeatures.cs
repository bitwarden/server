using System.Security.Cryptography;
using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Options;

namespace Bit.Seeder.Factories;

/// <summary>
/// Maps PlanType to organization feature flags.
/// Values sourced from MockPlans in test/Core.Test/Billing/Mocks/Plans/.
/// </summary>
public static class PlanFeatures
{
    internal static void Apply(Organization org, PlanType planType)
    {
        switch (planType)
        {
            case PlanType.Free:
                org.Plan = "Free";
                org.PlanType = PlanType.Free;
                org.MaxCollections = 2;
                org.MaxStorageGb = null;
                ApplyMinimalFeatures(org);
                break;

            case PlanType.TeamsMonthly:
                org.Plan = "Teams (Monthly)";
                org.PlanType = PlanType.TeamsMonthly;
                ApplyTeamsFeatures(org);
                break;

            case PlanType.TeamsAnnually:
                org.Plan = "Teams (Annually)";
                org.PlanType = PlanType.TeamsAnnually;
                ApplyTeamsFeatures(org);
                break;

            case PlanType.TeamsStarter:
                org.Plan = "Teams Starter";
                org.PlanType = PlanType.TeamsStarter;
                ApplyTeamsFeatures(org);
                break;

            case PlanType.EnterpriseMonthly:
                org.Plan = "Enterprise (Monthly)";
                org.PlanType = PlanType.EnterpriseMonthly;
                ApplyEnterpriseFeatures(org);
                break;

            case PlanType.EnterpriseAnnually:
                org.Plan = "Enterprise (Annually)";
                org.PlanType = PlanType.EnterpriseAnnually;
                ApplyEnterpriseFeatures(org);
                break;

            case PlanType.FamiliesAnnually:
                org.Plan = "Families";
                org.PlanType = PlanType.FamiliesAnnually;
                org.MaxCollections = null;
                org.MaxStorageGb = 1;
                ApplyMinimalFeatures(org);
                org.UseTotp = true;
                org.Use2fa = true;
                org.UsersGetPremium = true;
                break;

            default:
                throw new ArgumentException(
                    $"Unsupported PlanType '{planType}'. Supported types: Free, TeamsMonthly, TeamsAnnually, " +
                    "TeamsStarter, EnterpriseMonthly, EnterpriseAnnually, FamiliesAnnually.");
        }
    }

    /// <summary>
    /// Applies overrides on top of the organization's initial values.
    /// Only non-null properties are applied; null means "leave the value unchanged from <see cref="OrganizationSeeder.Create"/>".
    /// </summary>
    internal static void ApplyOrganizationOverrides(Organization org, OrganizationOverrides? overrides)
    {
        if (overrides is null)
        {
            return;
        }

        org.UseAutomaticUserConfirmation = overrides.UseAutomaticUserConfirmation ?? org.UseAutomaticUserConfirmation;
        org.AllowAdminAccessToAllCollectionItems = overrides.AllowAdminAccessToAllCollectionItems ?? org.AllowAdminAccessToAllCollectionItems;
        org.LimitItemDeletion = overrides.LimitItemDeletion ?? org.LimitItemDeletion;
        org.LimitCollectionCreation = overrides.LimitCollectionCreation ?? org.LimitCollectionCreation;
        org.LimitCollectionDeletion = overrides.LimitCollectionDeletion ?? org.LimitCollectionDeletion;
    }

    public static PlanType Parse(string? planTypeString)
    {
        if (string.IsNullOrEmpty(planTypeString))
        {
            return PlanType.EnterpriseAnnually;
        }

        return planTypeString switch
        {
            "free" => PlanType.Free,
            "teams-monthly" => PlanType.TeamsMonthly,
            "teams-annually" => PlanType.TeamsAnnually,
            "teams-starter" => PlanType.TeamsStarter,
            "enterprise-monthly" => PlanType.EnterpriseMonthly,
            "enterprise-annually" => PlanType.EnterpriseAnnually,
            "families-annually" => PlanType.FamiliesAnnually,
            _ => throw new ArgumentException(
                $"Invalid planType string '{planTypeString}'. Valid values: free, teams-monthly, " +
                "teams-annually, teams-starter, enterprise-monthly, enterprise-annually, families-annually.")
        };
    }

    /// <summary>
    /// Deterministic seat count from a log-normal distribution seeded by domain.
    /// Ranges sourced from our production data.
    /// </summary>
    internal static int GenerateRealisticSeatCount(PlanType planType, string domain)
    {
        var (min, max, avg) = planType switch
        {
            PlanType.Free => (1, 2, 2),
            PlanType.FamiliesAnnually => (6, 6, 6),
            PlanType.TeamsMonthly => (1, 300, 15),
            PlanType.TeamsAnnually => (1, 100, 7),
            PlanType.TeamsStarter => (10, 10, 10),
            PlanType.EnterpriseMonthly => (1, 185, 17),
            PlanType.EnterpriseAnnually => (1, 12000, 60),
            // Intentional: PlanType has 20+ variants including legacy plans. Seeder only models
            // the 7 current plans; all others get reasonable defaults. Not a bug.
            _ => (1, 100, 10)
        };

        if (min == max)
        {
            return min;
        }

        var logAvg = Math.Log(avg);
        var logMax = Math.Log(max);
        var sigma = (logMax - logAvg) / 2.0;
        var mu = logAvg - (sigma * sigma / 2.0);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(domain));
        var random = new Random(BitConverter.ToInt32(hashBytes, 0));

        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        var stdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        return Math.Clamp((int)Math.Round(Math.Exp(mu + sigma * stdNormal)), min, max);
    }

    /// <summary>
    /// Baseline: all plan-gated features off. Free and Families start here then enable selectively.
    /// </summary>
    private static void ApplyMinimalFeatures(Organization org)
    {
        org.UseGroups = false;
        org.UseDirectory = false;
        org.UseEvents = false;
        org.UseTotp = false;
        org.Use2fa = false;
        org.UseApi = false;
        org.UseScim = false;
        org.UseSso = false;
        org.UsePolicies = false;
        org.UseKeyConnector = false;
        org.UseResetPassword = false;
        org.UseCustomPermissions = false;
        org.UseOrganizationDomains = false;
        org.UsersGetPremium = false;
        org.SelfHost = false;
        org.UsePasswordManager = true;
        org.UseSecretsManager = false;
        org.UseRiskInsights = false;
        org.UseAdminSponsoredFamilies = false;
        org.SyncSeats = false;
        org.UseInviteLinks = false;
    }

    private static void ApplyTeamsFeatures(Organization org)
    {
        org.MaxCollections = null;
        org.MaxStorageGb = 1;
        org.UseGroups = true;
        org.UseDirectory = true;
        org.UseEvents = true;
        org.UseTotp = true;
        org.Use2fa = true;
        org.UseApi = true;
        org.UseScim = true;
        org.UseSso = false;
        org.UsePolicies = false;
        org.UseKeyConnector = false;
        org.UseResetPassword = false;
        org.UseCustomPermissions = false;
        org.UseOrganizationDomains = false;
        org.UsersGetPremium = true;
        org.SelfHost = false;
        org.UsePasswordManager = true;
        org.UseSecretsManager = true;
        org.UseRiskInsights = false;
        org.UseAdminSponsoredFamilies = false;
        org.SyncSeats = true;
        org.UseInviteLinks = false;
    }

    private static void ApplyEnterpriseFeatures(Organization org)
    {
        org.MaxCollections = null;
        org.MaxStorageGb = 1;
        org.UseGroups = true;
        org.UseDirectory = true;
        org.UseEvents = true;
        org.UseTotp = true;
        org.Use2fa = true;
        org.UseApi = true;
        org.UseScim = true;
        org.UseSso = true;
        org.UsePolicies = true;
        org.UseKeyConnector = true;
        org.UseResetPassword = true;
        org.UseCustomPermissions = true;
        org.UseOrganizationDomains = true;
        org.UsersGetPremium = true;
        org.SelfHost = true;
        org.UsePasswordManager = true;
        org.UseSecretsManager = true;
        org.UseRiskInsights = true;
        org.UseAdminSponsoredFamilies = true;
        org.SyncSeats = true;
        org.UseInviteLinks = true;
    }
}
