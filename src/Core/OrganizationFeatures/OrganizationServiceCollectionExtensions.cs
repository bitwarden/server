using Bit.Core.AdminConsole.OrganizationAuth;
using Bit.Core.AdminConsole.OrganizationAuth.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups;
using Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Import;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.GlobalSettings;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Organization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.OrganizationFeatures.OrganizationCollections;
using Bit.Core.OrganizationFeatures.OrganizationCollections.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures;

public static class OrganizationServiceCollectionExtensions
{
    public static void AddOrganizationServices(this IServiceCollection services, IGlobalSettings globalSettings)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddTokenizers();
        services.AddOrganizationGroupCommands();
        services.AddOrganizationConnectionCommands();
        services.AddOrganizationSponsorshipCommands(globalSettings);
        services.AddOrganizationApiKeyCommandsQueries();
        services.AddOrganizationCollectionCommands();
        services.AddOrganizationGroupCommands();
        services.AddOrganizationDomainCommandsQueries();
        services.AddOrganizationSignUpCommands();
        services.AddOrganizationDeleteCommands();
        services.AddOrganizationUpdateCommands();
        services.AddOrganizationEnableCommands();
        services.AddOrganizationDisableCommands();
        services.AddOrganizationAuthCommands();
        services.AddOrganizationUserCommands();
        services.AddOrganizationUserCommandsQueries();
        services.AddBaseOrganizationSubscriptionCommandsQueries();
    }

    private static void AddOrganizationSignUpCommands(this IServiceCollection services)
    {
        services.AddScoped<ICloudOrganizationSignUpCommand, CloudOrganizationSignUpCommand>();
        services.AddScoped<IProviderClientOrganizationSignUpCommand, ProviderClientOrganizationSignUpCommand>();
        services.AddScoped<IResellerClientOrganizationSignUpCommand, ResellerClientOrganizationSignUpCommand>();
        services.AddScoped<ISelfHostedOrganizationSignUpCommand, SelfHostedOrganizationSignUpCommand>();
    }

    private static void AddOrganizationDeleteCommands(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationDeleteCommand, OrganizationDeleteCommand>();
        services.AddScoped<IOrganizationInitiateDeleteCommand, OrganizationInitiateDeleteCommand>();
    }

    private static void AddOrganizationUpdateCommands(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationUpdateKeysCommand, OrganizationUpdateKeysCommand>();
    }

    private static void AddOrganizationEnableCommands(this IServiceCollection services) =>
        services.AddScoped<IOrganizationEnableCommand, OrganizationEnableCommand>();

    private static void AddOrganizationDisableCommands(this IServiceCollection services) =>
        services.AddScoped<IOrganizationDisableCommand, OrganizationDisableCommand>();

    private static void AddOrganizationConnectionCommands(this IServiceCollection services)
    {
        services.AddScoped<ICreateOrganizationConnectionCommand, CreateOrganizationConnectionCommand>();
        services.AddScoped<IDeleteOrganizationConnectionCommand, DeleteOrganizationConnectionCommand>();
        services.AddScoped<IUpdateOrganizationConnectionCommand, UpdateOrganizationConnectionCommand>();
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

    private static void AddOrganizationUserCommands(this IServiceCollection services)
    {
        services.AddScoped<IRemoveOrganizationUserCommand, RemoveOrganizationUserCommand>();
        services.AddScoped<IRevokeNonCompliantOrganizationUserCommand, RevokeNonCompliantOrganizationUserCommand>();
        services.AddScoped<IRevokeOrganizationUserCommand, RevokeOrganizationUserCommand>();
        services.AddScoped<IUpdateOrganizationUserCommand, UpdateOrganizationUserCommand>();
        services.AddScoped<IUpdateOrganizationUserGroupsCommand, UpdateOrganizationUserGroupsCommand>();
        services.AddScoped<IConfirmOrganizationUserCommand, ConfirmOrganizationUserCommand>();
        services.AddScoped<IAdminRecoverAccountCommand, AdminRecoverAccountCommand>();
        services.AddScoped<IAutomaticallyConfirmOrganizationUserCommand, AutomaticallyConfirmOrganizationUserCommand>();
        services.AddScoped<IAutomaticallyConfirmOrganizationUsersValidator, AutomaticallyConfirmOrganizationUsersValidator>();

        services.AddScoped<IDeleteClaimedOrganizationUserAccountCommand, DeleteClaimedOrganizationUserAccountCommand>();
        services.AddScoped<IDeleteClaimedOrganizationUserAccountValidator, DeleteClaimedOrganizationUserAccountValidator>();
    }

    private static void AddOrganizationApiKeyCommandsQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetOrganizationApiKeyQuery, GetOrganizationApiKeyQuery>();
        services.AddScoped<IRotateOrganizationApiKeyCommand, RotateOrganizationApiKeyCommand>();
        services.AddScoped<ICreateOrganizationApiKeyCommand, CreateOrganizationApiKeyCommand>();
    }

    public static void AddOrganizationCollectionCommands(this IServiceCollection services)
    {
        services.AddScoped<ICreateCollectionCommand, CreateCollectionCommand>();
        services.AddScoped<IUpdateCollectionCommand, UpdateCollectionCommand>();
        services.AddScoped<IDeleteCollectionCommand, DeleteCollectionCommand>();
        services.AddScoped<IBulkAddCollectionAccessCommand, BulkAddCollectionAccessCommand>();
    }

    private static void AddOrganizationGroupCommands(this IServiceCollection services)
    {
        services.AddScoped<ICreateGroupCommand, CreateGroupCommand>();
        services.AddScoped<IDeleteGroupCommand, DeleteGroupCommand>();
        services.AddScoped<IUpdateGroupCommand, UpdateGroupCommand>();
    }

    private static void AddOrganizationDomainCommandsQueries(this IServiceCollection services)
    {
        services.AddScoped<ICreateOrganizationDomainCommand, CreateOrganizationDomainCommand>();
        services.AddScoped<IVerifyOrganizationDomainCommand, VerifyOrganizationDomainCommand>();
        services.AddScoped<IGetOrganizationDomainByIdOrganizationIdQuery, GetOrganizationDomainByIdOrganizationIdQuery>();
        services.AddScoped<IGetOrganizationDomainByOrganizationIdQuery, GetOrganizationDomainByOrganizationIdQuery>();
        services.AddScoped<IDeleteOrganizationDomainCommand, DeleteOrganizationDomainCommand>();
        services.AddScoped<IOrganizationHasVerifiedDomainsQuery, OrganizationHasVerifiedDomainsQuery>();
    }

    private static void AddOrganizationAuthCommands(this IServiceCollection services)
    {
        services.AddScoped<IUpdateOrganizationAuthRequestCommand, UpdateOrganizationAuthRequestCommand>();
    }

    private static void AddOrganizationUserCommandsQueries(this IServiceCollection services)
    {
        services.AddScoped<ICountNewSmSeatsRequiredQuery, CountNewSmSeatsRequiredQuery>();
        services.AddScoped<IAcceptOrgUserCommand, AcceptOrgUserCommand>();
        services.AddScoped<IOrganizationUserUserDetailsQuery, OrganizationUserUserDetailsQuery>();
        services.AddScoped<IGetOrganizationUsersClaimedStatusQuery, GetOrganizationUsersClaimedStatusQuery>();

        services.AddScoped<IRestoreOrganizationUserCommand, RestoreOrganizationUserCommand>();

        services.AddScoped<IAuthorizationHandler, OrganizationUserUserDetailsAuthorizationHandler>();
        services.AddScoped<IHasConfirmedOwnersExceptQuery, HasConfirmedOwnersExceptQuery>();

        services.AddScoped<IInviteOrganizationUsersCommand, InviteOrganizationUsersCommand>();
        services.AddScoped<ISendOrganizationInvitesCommand, SendOrganizationInvitesCommand>();
        services.AddScoped<IResendOrganizationInviteCommand, ResendOrganizationInviteCommand>();

        services.AddScoped<IInviteUsersValidator, InviteOrganizationUsersValidator>();
        services.AddScoped<IInviteUsersOrganizationValidator, InviteUsersOrganizationValidator>();
        services.AddScoped<IInviteUsersPasswordManagerValidator, InviteUsersPasswordManagerValidator>();
        services.AddScoped<IInviteUsersEnvironmentValidator, InviteUsersEnvironmentValidator>();
        services.AddScoped<IInitPendingOrganizationCommand, InitPendingOrganizationCommand>();
        services.AddScoped<IImportOrganizationUsersAndGroupsCommand, ImportOrganizationUsersAndGroupsCommand>();
    }

    // TODO: move to OrganizationSubscriptionServiceCollectionExtensions when OrganizationUser methods are moved out of
    // TODO: OrganizationService - see PM-1880
    private static void AddBaseOrganizationSubscriptionCommandsQueries(this IServiceCollection services)
    {
        services.AddScoped<IUpdateSecretsManagerSubscriptionCommand, UpdateSecretsManagerSubscriptionCommand>();
    }

    private static void AddTokenizers(this IServiceCollection services)
    {
        services.AddSingleton<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>(serviceProvider =>
            new DataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>(
                OrganizationSponsorshipOfferTokenable.ClearTextPrefix,
                OrganizationSponsorshipOfferTokenable.DataProtectorPurpose,
                serviceProvider.GetDataProtectionProvider(),
                serviceProvider.GetRequiredService<ILogger<DataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>>())
        );
    }
}
