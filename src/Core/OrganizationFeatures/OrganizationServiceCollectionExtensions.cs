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
        public static void AddOrganizationServices(this IServiceCollection services)
        {
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<IOrganizationApiKeyService, OrganizationApiKeyService>();
            services.AddTokenizers();
            services.AddOrganizationSponsorshipCommands();
        }

        private static void AddOrganizationSponsorshipCommands(this IServiceCollection services)
        {
            services.AddScoped<ICreateSponsorshipCommand, CreateSponsorshipCommand>();

            services.AddScoped<IRemoveSponsorshipCommand, RemoveSponsorshipCommand>();

            services.AddScoped<ISendSponsorshipOfferCommand, SendSponsorshipOfferCommand>();

            services.AddScoped<ICloudRevokeSponsorshipCommand, CloudRevokeSponsorshipCommand>();
            services.AddScoped<ISelfHostedRevokeSponsorshipCommand, SelfHostedRevokeSponsorshipCommand>();

            services.AddScoped<ISetUpSponsorshipCommand, SetUpSponsorshipCommand>();

            services.AddScoped<IValidateRedemptionTokenCommand, ValidateRedemptionTokenCommand>();

            services.AddScoped<IValidateSponsorshipCommand, ValidateSponsorshipCommand>();
        }

        private static void AddTokenizers(this IServiceCollection services)
        {
            services.AddSingleton<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>(serviceProvider =>
                new DataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>(
                    OrganizationSponsorshipOfferTokenable.ClearTextPrefix,
                    OrganizationSponsorshipOfferTokenable.DataProtectorPurpose,
                    serviceProvider.GetDataProtectionProvider())
            );

            services.AddSingleton<IDataProtectorTokenFactory<OrganizationApiKeyTokenable>>(serviceProvider =>
                new DataProtectorTokenFactory<OrganizationApiKeyTokenable>(
                    OrganizationApiKeyTokenable.ClearTextPrefix,
                    OrganizationApiKeyTokenable.DataProtectorPurpose,
                    serviceProvider.GetDataProtectionProvider())
            );
        }
    }
}
