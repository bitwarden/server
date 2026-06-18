using Bit.Commercial.Pam.Engine;
using Bit.Commercial.Pam.OrganizationFeatures.Commands;
using Bit.Commercial.Pam.OrganizationFeatures.Queries;
using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Commercial.Pam.Services;
using Bit.Pam.Engine;
using Bit.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Pam.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Commercial.Pam.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddCommercialPamServices(this IServiceCollection services)
    {
        services.AddSingleton<IAccessRuleValidator, AccessRuleValidator>();
        services.AddScoped<ICreateAccessRuleCommand, CreateAccessRuleCommand>();
        services.AddScoped<IUpdateAccessRuleCommand, UpdateAccessRuleCommand>();
        services.AddScoped<IDeleteAccessRuleCommand, DeleteAccessRuleCommand>();
        services.AddScoped<IGoverningRuleResolver, GoverningRuleResolver>();
        services.AddScoped<ISingleActiveLeaseEvaluator, SingleActiveLeaseEvaluator>();
        services.AddSingleton<IAccessRuleEngine, AccessRuleEngine>();
        services.AddScoped<IAccessPreCheckQuery, AccessPreCheckQuery>();
        services.AddScoped<IGetLeasedCipherQuery, GetLeasedCipherQuery>();
        services.AddScoped<ISubmitAccessRequestCommand, SubmitAccessRequestCommand>();
        services.AddScoped<IApproverCollectionAccessQuery, ApproverCollectionAccessQuery>();
        services.AddScoped<IApproverInboxNotifier, ApproverInboxNotifier>();
        services.AddScoped<IRequesterNotifier, RequesterNotifier>();
        services.AddScoped<IListInboxRequestsQuery, ListInboxRequestsQuery>();
        services.AddScoped<IListInboxHistoryQuery, ListInboxHistoryQuery>();
        services.AddScoped<IDecideAccessRequestCommand, DecideAccessRequestCommand>();
        services.AddScoped<IActivateAccessRequestCommand, ActivateAccessRequestCommand>();
        services.AddScoped<ICancelAccessRequestCommand, CancelAccessRequestCommand>();
        services.AddScoped<IRequestLeaseExtensionCommand, RequestLeaseExtensionCommand>();
        services.AddScoped<IRevokeAccessLeaseCommand, RevokeAccessLeaseCommand>();
        services.AddScoped<IGetCipherAccessStateQuery, GetCipherAccessStateQuery>();
        services.AddScoped<IListMyAccessRequestsQuery, ListMyAccessRequestsQuery>();
        services.AddScoped<IListMyActiveAccessLeasesQuery, ListMyActiveAccessLeasesQuery>();
        services.AddScoped<IListActiveLeasesQuery, ListActiveLeasesQuery>();
        services.AddScoped<IListLeaseHistoryQuery, ListLeaseHistoryQuery>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICipherLeaseGate, CipherLeaseGate>();
    }
}
