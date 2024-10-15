using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.Licenses.OrganizationLicenses;

public class CloudGetOrganizationLicenseQuery(
    IInstallationRepository installationRepository,
    IPaymentService paymentService,
    ILicensingService licensingService)
    : ICloudGetOrganizationLicenseQuery
{
    public async Task<OrganizationLicense> GetLicenseAsync(
        Organization organization,
        Guid installationId,
        int? version = null)
    {
        var installation = await installationRepository.GetByIdAsync(installationId);
        if (installation is not { Enabled: true })
        {
            throw new BadRequestException("Invalid installation id");
        }

        var subscriptionInfo = await paymentService.GetSubscriptionAsync(organization);
        var now = DateTime.UtcNow;
        var expirationDate = GetExpirationDate(subscriptionInfo, organization, now);
        var expirationWithoutGracePeriodDate = GetExpirationWithoutGracePeriodDate(subscriptionInfo, organization, now);
        var refreshDate = GetRefreshDate(subscriptionInfo, organization, expirationDate, now);
        var isTrial = IsTrial(subscriptionInfo, organization, now);

        var organizationLicense = new OrganizationLicense
        {
            Version = version.GetValueOrDefault(OrganizationLicense.CurrentLicenseFileVersion),
            LicenseKey = organization.LicenseKey,
            InstallationId = installationId,
            Id = organization.Id,
            Name = organization.Name,
            BillingEmail = organization.BillingEmail,
            BusinessName = organization.BusinessName,
            Enabled = organization.Enabled,
            Plan = organization.Plan,
            PlanType = organization.PlanType,
            Seats = organization.Seats,
            MaxCollections = organization.MaxCollections,
            UsePolicies = organization.UsePolicies,
            UseSso = organization.UseSso,
            UseKeyConnector = organization.UseKeyConnector,
            UseScim = organization.UseScim,
            UseGroups = organization.UseGroups,
            UseEvents = organization.UseEvents,
            UseDirectory = organization.UseDirectory,
            UseTotp = organization.UseTotp,
            Use2fa = organization.Use2fa,
            UseApi = organization.UseApi,
            UseResetPassword = organization.UseResetPassword,
            MaxStorageGb = organization.MaxStorageGb,
            SelfHost = organization.SelfHost,
            UsersGetPremium = organization.UsersGetPremium,
            UseCustomPermissions = organization.UseCustomPermissions,
            UsePasswordManager = organization.UsePasswordManager,
            UseSecretsManager = organization.UseSecretsManager,
            SmSeats = organization.SmSeats,
            SmServiceAccounts = organization.SmServiceAccounts,
            LimitCollectionCreationDeletion = organization.LimitCollectionCreationDeletion,
            AllowAdminAccessToAllCollectionItems = organization.AllowAdminAccessToAllCollectionItems,
            Issued = now,
            Expires = expirationDate,
            ExpirationWithoutGracePeriod = expirationWithoutGracePeriodDate,
            Refresh = refreshDate,
            Trial = isTrial
        };

        // Hash must come after all properties are set, and before Signature since Signature contains the hash
        organizationLicense.Hash = Convert.ToBase64String(organizationLicense.EncodedHash);
        organizationLicense.Signature = Convert.ToBase64String(licensingService.SignLicense(organizationLicense));
        organizationLicense.Token = await licensingService.GenerateToken(organizationLicense);

        return organizationLicense;
    }

    private static DateTime GetExpirationDate(SubscriptionInfo subscriptionInfo, Organization org, DateTime now)
    {
        if (subscriptionInfo?.Subscription == null)
        {
            return org.PlanType == PlanType.Custom && org.ExpirationDate.HasValue
                ? org.ExpirationDate.Value
                : now.AddDays(7);
        }

        var subscription = subscriptionInfo.Subscription;

        if (subscription.TrialEndDate > now)
        {
            return subscription.TrialEndDate.Value;
        }

        if (org.ExpirationDate < now)
        {
            return org.ExpirationDate.Value;
        }

        if (subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return subscription.PeriodEndDate
                !.Value
                .AddDays(Bit.Core.Constants.OrganizationSelfHostSubscriptionGracePeriodDays);
        }

        return org.ExpirationDate?.AddMonths(11) ?? now.AddYears(1);
    }

    private static DateTime? GetExpirationWithoutGracePeriodDate(SubscriptionInfo subscriptionInfo, Organization org, DateTime now)
    {
        var isNotTrialing = !subscriptionInfo.Subscription.TrialEndDate.HasValue || subscriptionInfo.Subscription.TrialEndDate.Value <= DateTime.UtcNow;
        var orgHasNotExpired = !org.ExpirationDate.HasValue || org.ExpirationDate.Value >= DateTime.UtcNow;
        var isAnnualPlan = subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180);

        return isNotTrialing && orgHasNotExpired && isAnnualPlan
            ? subscriptionInfo.Subscription.PeriodEndDate
            : null;
    }

    private static DateTime GetRefreshDate(SubscriptionInfo subscriptionInfo, Organization org, DateTime licenseExpirationDate, DateTime now)
    {
        if (subscriptionInfo?.Subscription == null ||
            subscriptionInfo.Subscription.TrialEndDate > now ||
            org.ExpirationDate < now)
        {
            return licenseExpirationDate;
        }

        if (subscriptionInfo.Subscription.PeriodDuration > TimeSpan.FromDays(180))
        {
            return now.AddDays(30);
        }

        return now - licenseExpirationDate > TimeSpan.FromDays(30)
            ? now.AddDays(30)
            : licenseExpirationDate;
    }

    private static bool IsTrial(SubscriptionInfo subscriptionInfo, Organization org, DateTime now) =>
        subscriptionInfo?.Subscription == null
            ? org.PlanType != PlanType.Custom || !org.ExpirationDate.HasValue
            : subscriptionInfo.Subscription.TrialEndDate > now;
}
