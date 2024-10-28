using System.Globalization;
using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Licenses.Models;
using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Licenses.Services.Implementations;

public class OrganizationLicenseClaimsFactory : ILicenseClaimsFactory<Organization>
{
    public Task<List<Claim>> GenerateClaims(Organization entity, LicenseContext licenseContext)
    {
        var subscriptionInfo = licenseContext.SubscriptionInfo;
        var expires = entity.CalculateFreshExpirationDate(subscriptionInfo);
        var refresh = entity.CalculateFreshRefreshDate(subscriptionInfo, expires);
        var expirationWithoutGracePeriod = entity.CalculateFreshExpirationDateWithoutGracePeriod(subscriptionInfo, expires);
        var trial = IsTrialing(entity, subscriptionInfo);

        var claims = new List<Claim>
        {
            new(nameof(OrganizationLicense.LicenseType), LicenseType.Organization.ToString()),
            new(nameof(OrganizationLicense.InstallationId), licenseContext.InstallationId.ToString()),
            new(nameof(OrganizationLicense.Id), entity.Id.ToString()),
            new(nameof(OrganizationLicense.Name), entity.Name),
            new(nameof(OrganizationLicense.BillingEmail), entity.BillingEmail),
            new(nameof(OrganizationLicense.Enabled), entity.Enabled.ToString()),
            new(nameof(OrganizationLicense.Plan), entity.Plan),
            new(nameof(OrganizationLicense.PlanType), entity.PlanType.ToString()),
            new(nameof(OrganizationLicense.Seats), entity.Seats.ToString()),
            new(nameof(OrganizationLicense.MaxCollections), entity.MaxCollections.ToString()),
            new(nameof(OrganizationLicense.UsePolicies), entity.UsePolicies.ToString()),
            new(nameof(OrganizationLicense.UseSso), entity.UseSso.ToString()),
            new(nameof(OrganizationLicense.UseKeyConnector), entity.UseKeyConnector.ToString()),
            new(nameof(OrganizationLicense.UseScim), entity.UseScim.ToString()),
            new(nameof(OrganizationLicense.UseGroups), entity.UseGroups.ToString()),
            new(nameof(OrganizationLicense.UseEvents), entity.UseEvents.ToString()),
            new(nameof(OrganizationLicense.UseDirectory), entity.UseDirectory.ToString()),
            new(nameof(OrganizationLicense.UseTotp), entity.UseTotp.ToString()),
            new(nameof(OrganizationLicense.Use2fa), entity.Use2fa.ToString()),
            new(nameof(OrganizationLicense.UseApi), entity.UseApi.ToString()),
            new(nameof(OrganizationLicense.UseResetPassword), entity.UseResetPassword.ToString()),
            new(nameof(OrganizationLicense.MaxStorageGb), entity.MaxStorageGb.ToString()),
            new(nameof(OrganizationLicense.SelfHost), entity.SelfHost.ToString()),
            new(nameof(OrganizationLicense.UsersGetPremium), entity.UsersGetPremium.ToString()),
            new(nameof(OrganizationLicense.UseCustomPermissions), entity.UseCustomPermissions.ToString()),
            new(nameof(OrganizationLicense.Issued), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)),
            new(nameof(OrganizationLicense.UsePasswordManager), entity.UsePasswordManager.ToString()),
            new(nameof(OrganizationLicense.UseSecretsManager), entity.UseSecretsManager.ToString()),
            new(nameof(OrganizationLicense.SmSeats), entity.SmSeats.ToString()),
            new(nameof(OrganizationLicense.SmServiceAccounts), entity.SmServiceAccounts.ToString()),
            new(nameof(OrganizationLicense.LimitCollectionCreationDeletion), entity.LimitCollectionCreationDeletion.ToString()),
            new(nameof(OrganizationLicense.AllowAdminAccessToAllCollectionItems), entity.AllowAdminAccessToAllCollectionItems.ToString()),
            new(nameof(OrganizationLicense.Expires), expires.ToString(CultureInfo.InvariantCulture)),
            new(nameof(OrganizationLicense.Refresh), refresh.ToString(CultureInfo.InvariantCulture)),
            new(nameof(OrganizationLicense.ExpirationWithoutGracePeriod), expirationWithoutGracePeriod.ToString(CultureInfo.InvariantCulture)),
            new(nameof(OrganizationLicense.Trial), trial.ToString()),
        };

        if (entity.LicenseKey is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicense.LicenseKey), entity.LicenseKey));
        }

        if (entity.BusinessName is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicense.BusinessName), entity.BusinessName));
        }

        return Task.FromResult(claims);
    }

    private static bool IsTrialing(Organization org, SubscriptionInfo subscriptionInfo) =>
        subscriptionInfo?.Subscription is null
            ? org.PlanType != PlanType.Custom || !org.ExpirationDate.HasValue
            : subscriptionInfo.Subscription.TrialEndDate > DateTime.UtcNow;
}
