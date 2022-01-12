using Bit.Core.OrganizationFeatures.OrgUser;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation.Accept;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation.Confirm;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation.Invite;
using Bit.Core.OrganizationFeatures.OrgUser.Invitation.ResendInvite;
using Bit.Core.OrganizationFeatures.OrgUser.Mail;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.OrganizationFeatures
{
    public static class OrganizationServiceCollectionExtensions
    {
        public static void AddOrganizationServices(this IServiceCollection services)
        {
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<IOrganizationUserAccessPolicies, OrganizationUserAccessPolicies>();
            services.AddScoped<IOrganizationUserAcceptAccessPolicies, OrganizationUserAcceptAccessPolicies>();
            services.AddScoped<IOrganizationUserAcceptCommand, OrganizationUserAcceptCommand>();
            services.AddScoped<IOrganizationUserConfirmAccessPolicies, OrganizationUserConfirmAccessPolicies>();
            services.AddScoped<IOrganizationUserConfirmCommand, OrganizationUserConfirmCommand>();
            services.AddScoped<IOrganizationUserInviteAccessPolicies, OrganizationUserInviteAccessPolicies>();
            services.AddScoped<IOrganizationUserInviteCommand, OrganizationUserInviteCommand>();
            services.AddScoped<IOrganizationUserResendInviteAccessPolicies, OrganizationUserResendInviteAccessPolicies>();
            services.AddScoped<IOrganizationUserResendInviteCommand, OrganizationUserResendInviteCommand>();
            services.AddScoped<IOrganizationUserMailer, OrganizationUserMailer>();
            services.AddScoped<IOrganizationUserInvitationService, OrganizationUserInvitationService>();
            services.AddScoped<IOrganizationSponsorshipService, OrganizationSponsorshipService>();
        }
    }
}
