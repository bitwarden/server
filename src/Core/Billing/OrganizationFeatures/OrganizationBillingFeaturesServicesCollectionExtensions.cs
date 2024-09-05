using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Billing.OrganizationFeatures.OrganizationLicenses;
using Bit.Core.Billing.OrganizationFeatures.OrganizationLicenses.Cloud;
using Bit.Core.Billing.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.Billing.OrganizationFeatures.OrganizationLicenses.SelfHosted;
using Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;
using Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;
using Bit.Core.Billing.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.Billing.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.OrganizationFeatures;

public static class OrganizationBillingFeaturesServicesCollectionExtensions
{
    public static void AddOrganizationBillingFeaturesServices(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddOrganizationSponsorshipCommands(globalSettings);
        services.AddOrganizationLicenseCommandsQueries();
        services.AddBaseOrganizationSubscriptionCommandsQueries();
    }

    private static void AddOrganizationSponsorshipCommands(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddScoped<ICreateSponsorshipCommand, CreateSponsorshipCommand>();
        services.AddScoped<IRemoveSponsorshipCommand, RemoveSponsorshipCommand>();
        services.AddScoped<ISendSponsorshipOfferCommand, SendSponsorshipOfferCommand>();
        services.AddScoped<ISetUpSponsorshipCommand, SetUpSponsorshipCommand>();
        services.AddScoped<IValidateRedemptionTokenCommand, ValidateRedemptionTokenCommand>();
        services.AddScoped<IValidateSponsorshipCommand, ValidateSponsorshipCommand>();
        services.AddScoped<IValidateBillingSyncKeyCommand, ValidateBillingSyncKeyCommand>();
        services.AddScoped<IOrganizationSponsorshipRenewCommand, OrganizationSponsorshipRenewCommand>();
        services.AddScoped<ICloudSyncSponsorshipsCommand, CloudSyncSponsorshipsCommand>();
        services.AddScoped<ISelfHostedSyncSponsorshipsCommand, SelfHostedSyncSponsorshipsCommand>();
        services.AddScoped<ISelfHostedSyncSponsorshipsCommand, SelfHostedSyncSponsorshipsCommand>();
        services.AddScoped<ICloudSyncSponsorshipsCommand, CloudSyncSponsorshipsCommand>();
        services.AddScoped<IValidateBillingSyncKeyCommand, ValidateBillingSyncKeyCommand>();
        if (globalSettings.SelfHosted)
        {
            services.AddScoped<IRevokeSponsorshipCommand, SelfHostedRevokeSponsorshipCommand>();
        }
        else
        {
            services.AddScoped<IRevokeSponsorshipCommand, CloudRevokeSponsorshipCommand>();
        }
    }

    private static void AddOrganizationLicenseCommandsQueries(this IServiceCollection services)
    {
        services.AddScoped<ICloudGetOrganizationLicenseQuery, CloudGetOrganizationLicenseQuery>();
        services.AddScoped<ISelfHostedGetOrganizationLicenseQuery, SelfHostedGetOrganizationLicenseQuery>();
        services.AddScoped<IUpdateOrganizationLicenseCommand, UpdateOrganizationLicenseCommand>();
    }

    // TODO: move to OrganizationSubscriptionServiceCollectionExtensions when OrganizationUser methods are moved out of
    // TODO: OrganizationService - see PM-1880
    private static void AddBaseOrganizationSubscriptionCommandsQueries(this IServiceCollection services)
    {
        services.AddScoped<IUpdateSecretsManagerSubscriptionCommand, UpdateSecretsManagerSubscriptionCommand>();
    }
}
