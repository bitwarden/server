﻿using System.Globalization;
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
            new(nameof(OrganizationLicenseConstants.LicenseType), LicenseType.Organization.ToString()),
            new(nameof(OrganizationLicenseConstants.Id), entity.Id.ToString()),
            new(nameof(OrganizationLicenseConstants.Enabled), entity.Enabled.ToString()),
            new(nameof(OrganizationLicenseConstants.PlanType), entity.PlanType.ToString()),
            new(nameof(OrganizationLicenseConstants.UsePolicies), entity.UsePolicies.ToString()),
            new(nameof(OrganizationLicenseConstants.UseSso), entity.UseSso.ToString()),
            new(nameof(OrganizationLicenseConstants.UseKeyConnector), entity.UseKeyConnector.ToString()),
            new(nameof(OrganizationLicenseConstants.UseScim), entity.UseScim.ToString()),
            new(nameof(OrganizationLicenseConstants.UseGroups), entity.UseGroups.ToString()),
            new(nameof(OrganizationLicenseConstants.UseEvents), entity.UseEvents.ToString()),
            new(nameof(OrganizationLicenseConstants.UseDirectory), entity.UseDirectory.ToString()),
            new(nameof(OrganizationLicenseConstants.UseTotp), entity.UseTotp.ToString()),
            new(nameof(OrganizationLicenseConstants.Use2fa), entity.Use2fa.ToString()),
            new(nameof(OrganizationLicenseConstants.UseApi), entity.UseApi.ToString()),
            new(nameof(OrganizationLicenseConstants.UseResetPassword), entity.UseResetPassword.ToString()),
            new(nameof(OrganizationLicenseConstants.SelfHost), entity.SelfHost.ToString()),
            new(nameof(OrganizationLicenseConstants.UsersGetPremium), entity.UsersGetPremium.ToString()),
            new(nameof(OrganizationLicenseConstants.UseCustomPermissions), entity.UseCustomPermissions.ToString()),
            new(nameof(OrganizationLicenseConstants.UsePasswordManager), entity.UsePasswordManager.ToString()),
            new(nameof(OrganizationLicenseConstants.UseSecretsManager), entity.UseSecretsManager.ToString()),
            // LimitCollectionCreationDeletion was split and removed from the
            // license. Left here with an assignment from the new values for
            // backwards compatibility.
            new(nameof(OrganizationLicenseConstants.LimitCollectionCreationDeletion),
                (entity.LimitCollectionCreation || entity.LimitCollectionDeletion).ToString()),
            new(nameof(OrganizationLicenseConstants.AllowAdminAccessToAllCollectionItems), entity.AllowAdminAccessToAllCollectionItems.ToString()),
            new(nameof(OrganizationLicenseConstants.UseRiskInsights), entity.UseRiskInsights.ToString()),
            new(nameof(OrganizationLicenseConstants.Issued), DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)),
            new(nameof(OrganizationLicenseConstants.Expires), expires.ToString(CultureInfo.InvariantCulture)),
            new(nameof(OrganizationLicenseConstants.Refresh), refresh.ToString(CultureInfo.InvariantCulture)),
            new(nameof(OrganizationLicenseConstants.ExpirationWithoutGracePeriod), expirationWithoutGracePeriod.ToString(CultureInfo.InvariantCulture)),
            new(nameof(OrganizationLicenseConstants.Trial), trial.ToString()),
            new(nameof(OrganizationLicenseConstants.UseAdminSponsoredFamilies), entity.UseAdminSponsoredFamilies.ToString()),
        };

        if (entity.Name is not null)
        {
            claims.Add(new(nameof(OrganizationLicenseConstants.Name), entity.Name));
        }

        if (entity.BillingEmail is not null)
        {
            claims.Add(new(nameof(OrganizationLicenseConstants.BillingEmail), entity.BillingEmail));
        }

        if (entity.Plan is not null)
        {
            claims.Add(new(nameof(OrganizationLicenseConstants.Plan), entity.Plan));
        }

        if (entity.BusinessName is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicenseConstants.BusinessName), entity.BusinessName));
        }

        if (entity.LicenseKey is not null)
        {
            claims.Add(new Claim(nameof(OrganizationLicenseConstants.LicenseKey), entity.LicenseKey));
        }

        if (licenseContext.InstallationId.HasValue)
        {
            claims.Add(new Claim(nameof(OrganizationLicenseConstants.InstallationId), licenseContext.InstallationId.ToString()));
        }

        if (entity.Seats.HasValue)
        {
            claims.Add(new Claim(nameof(OrganizationLicenseConstants.Seats), entity.Seats.ToString()));
        }

        if (entity.MaxCollections.HasValue)
        {
            claims.Add(new Claim(nameof(OrganizationLicenseConstants.MaxCollections), entity.MaxCollections.ToString()));
        }

        if (entity.MaxStorageGb.HasValue)
        {
            claims.Add(new Claim(nameof(OrganizationLicenseConstants.MaxStorageGb), entity.MaxStorageGb.ToString()));
        }

        if (entity.SmSeats.HasValue)
        {
            claims.Add(new Claim(nameof(OrganizationLicenseConstants.SmSeats), entity.SmSeats.ToString()));
        }

        if (entity.SmServiceAccounts.HasValue)
        {
            claims.Add(new Claim(nameof(OrganizationLicenseConstants.SmServiceAccounts), entity.SmServiceAccounts.ToString()));
        }
        claims.Add(new Claim(nameof(OrganizationLicenseConstants.UseAdminSponsoredFamilies), entity.UseAdminSponsoredFamilies.ToString()));

        return Task.FromResult(claims);
    }

    private static bool IsTrialing(Organization org, SubscriptionInfo subscriptionInfo) =>
        subscriptionInfo?.Subscription is null
            ? org.PlanType != PlanType.Custom || !org.ExpirationDate.HasValue
            : subscriptionInfo.Subscription.TrialEndDate > DateTime.UtcNow;
}
