using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.OrganizationFeatures
{
    public static class OrganizationServiceCollectionExtensions
    {
        public static void AddOrganizationServices(this IServiceCollection services, IGlobalSettings globalSettings)
        {
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddTokenizers();
            services.AddOrganizationSponsorshipCommands(globalSettings);
        }

        private static void AddOrganizationSponsorshipCommands(this IServiceCollection services, IGlobalSettings globalSettings)
        {
            services.AddScoped<ICreateSponsorshipCommand, CreateSponsorshipCommand>();

            services.AddScoped<IRemoveSponsorshipCommand, CloudRemoveSponsorshipCommand>();

            services.AddScoped<ISendSponsorshipOfferCommand, CloudSendSponsorshipOfferCommand>();

            services.AddScoped<ICloudRevokeSponsorshipCommand, CloudRevokeSponsorshipCommand>();
            services.AddScoped<ISelfHostedRevokeSponsorshipCommand, SelfHostedRevokeSponsorshipCommand>();

            services.AddScoped<ISetUpSponsorshipCommand, CloudSetUpSponsorshipCommand>();

            services.AddScoped<IValidateRedemptionTokenCommand, ValidateRedemptionTokenCommand>();

            services.AddScoped<IValidateSponsorshipCommand, CloudValidateSponsorshipCommand>();
        }

        private static void AddTokenizers(this IServiceCollection services)
        {
            services.AddSingleton<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>(serviceProvider =>
                new DataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>(
                    OrganizationSponsorshipOfferTokenable.ClearTextPrefix,
                    OrganizationSponsorshipOfferTokenable.DataProtectorPurpose,
                    serviceProvider.GetDataProtectionProvider())
            );
        }
    }
}
