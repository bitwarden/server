using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;
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
            services.AddTokenizers();
            services.AddOrganizationSponsorshipCommands();
            services.AddOrganizationApiKeyCommands();
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

        private static void AddOrganizationApiKeyCommands(this IServiceCollection services)
        {
            services.AddScoped<IGetOrganizationApiKeyCommand, GetOrganizationApiKeyCommand>();
            services.AddScoped<IRotateOrganizationApiKeyCommand, RotateOrganizationApiKeyCommand>();
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
