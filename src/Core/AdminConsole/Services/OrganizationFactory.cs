using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Services;

public static class OrganizationFactory
{
    public static Organization Create(
        User owner,
        ClaimsPrincipal claimsPrincipal,
        string publicKey,
        string privateKey) => new()
        {
            Name = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.Name),
            BillingEmail = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.BillingEmail),
            BusinessName = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.BusinessName),
            PlanType = claimsPrincipal.GetValue<PlanType>(OrganizationLicenseConstants.PlanType),
            Seats = claimsPrincipal.GetValue<int?>(OrganizationLicenseConstants.Seats),
            MaxCollections = claimsPrincipal.GetValue<short?>(OrganizationLicenseConstants.MaxCollections),
            MaxStorageGb = Constants.SelfHostedMaxStorageGb,
            UsePolicies = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UsePolicies),
            UseSso = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseSso),
            UseKeyConnector = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseKeyConnector),
            UseScim = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseScim),
            UseGroups = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseGroups),
            UseDirectory = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseDirectory),
            UseEvents = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseEvents),
            UseTotp = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseTotp),
            Use2fa = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.Use2fa),
            UseApi = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseApi),
            UseResetPassword = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseResetPassword),
            Plan = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.Plan),
            SelfHost = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.SelfHost),
            UsersGetPremium = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UsersGetPremium),
            UseCustomPermissions =
            claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseCustomPermissions),
            Gateway = null,
            GatewayCustomerId = null,
            GatewaySubscriptionId = null,
            ReferenceData = owner.ReferenceData,
            Enabled = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.Enabled),
            ExpirationDate = claimsPrincipal.GetValue<DateTime?>(OrganizationLicenseConstants.Expires),
            LicenseKey = claimsPrincipal.GetValue<string>(OrganizationLicenseConstants.LicenseKey),
            PublicKey = publicKey,
            PrivateKey = privateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created,
            UsePasswordManager = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UsePasswordManager),
            UseSecretsManager = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseSecretsManager),
            SmSeats = claimsPrincipal.GetValue<int?>(OrganizationLicenseConstants.SmSeats),
            SmServiceAccounts = claimsPrincipal.GetValue<int?>(OrganizationLicenseConstants.SmServiceAccounts),
            UseRiskInsights = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseRiskInsights),
            UseOrganizationDomains =
            claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseOrganizationDomains),
            UseAdminSponsoredFamilies =
            claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseAdminSponsoredFamilies),
            UseAutomaticUserConfirmation = claimsPrincipal.GetValue<bool>(OrganizationLicenseConstants.UseAutomaticUserConfirmation),
        };

    public static Organization Create(
        User owner,
        OrganizationLicense license,
        string publicKey,
        string privateKey) => new()
        {
            Name = license.Name,
            BillingEmail = license.BillingEmail,
            BusinessName = license.BusinessName,
            PlanType = license.PlanType,
            Seats = license.Seats,
            MaxCollections = license.MaxCollections,
            MaxStorageGb = Constants.SelfHostedMaxStorageGb,
            UsePolicies = license.UsePolicies,
            UseSso = license.UseSso,
            UseKeyConnector = license.UseKeyConnector,
            UseScim = license.UseScim,
            UseGroups = license.UseGroups,
            UseDirectory = license.UseDirectory,
            UseEvents = license.UseEvents,
            UseTotp = license.UseTotp,
            Use2fa = license.Use2fa,
            UseApi = license.UseApi,
            UseResetPassword = license.UseResetPassword,
            Plan = license.Plan,
            SelfHost = license.SelfHost,
            UsersGetPremium = license.UsersGetPremium,
            UseCustomPermissions = license.UseCustomPermissions,
            Gateway = null,
            GatewayCustomerId = null,
            GatewaySubscriptionId = null,
            ReferenceData = owner.ReferenceData,
            Enabled = license.Enabled,
            ExpirationDate = license.Expires,
            LicenseKey = license.LicenseKey,
            PublicKey = publicKey,
            PrivateKey = privateKey,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
            Status = OrganizationStatusType.Created,
            UsePasswordManager = license.UsePasswordManager,
            UseSecretsManager = license.UseSecretsManager,
            SmSeats = license.SmSeats,
            SmServiceAccounts = license.SmServiceAccounts,
            UseRiskInsights = license.UseRiskInsights,
            UseOrganizationDomains = license.UseOrganizationDomains,
            UseAdminSponsoredFamilies = license.UseAdminSponsoredFamilies,
        };
}
