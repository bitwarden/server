using Bit.HttpExtensions;
using Bit.Pam.Services;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Services.Pam.Utilities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPamServices(this IServiceCollection services)
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
        services.AddScoped<IGetAccessRequestDetailsQuery, GetAccessRequestDetailsQuery>();
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
        services.AddScoped<IListAccessAuditTrailQuery, ListAccessAuditTrailQuery>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICipherLeaseGate, CipherLeaseGate>();
        services.AddScoped<IAccessAuditEventEmitter, AccessAuditEventEmitter>();

        // Minimal API endpoint handlers. The endpoints (see PamEndpointsExtensions) resolve these from DI.
        services.AddScoped<LeaseEndpointsHandler>();
        services.AddScoped<AccessRequestEndpointsHandler>();
        services.AddScoped<AccessRuleEndpointsHandler>();
        services.AddScoped<CipherLeaseEndpointsHandler>();

        // Rule evaluation engine. Pure and stateless, so a singleton is safe.
        services.AddSingleton<IAccessRuleEngine, AccessRuleEngine>();

        // Resolves the access rule governing a cipher for a caller, then evaluates it via the engine.
        services.AddScoped<IGoverningRuleResolver, GoverningRuleResolver>();

        // AccessRule write path.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAccessRuleValidator, AccessRuleValidator>();
        services.AddScoped<IAccessRuleWriteValidator, AccessRuleWriteValidator>();
        services.AddScoped<ICreateAccessRuleCommand, CreateAccessRuleCommand>();
        services.AddScoped<IUpdateAccessRuleCommand, UpdateAccessRuleCommand>();
        services.AddScoped<IDeleteAccessRuleCommand, DeleteAccessRuleCommand>();

        services.AddPamOpenApiEndpointDataSource();

        return services;
    }

    /// <summary>
    /// Registers the PAM Minimal API endpoints (see <c>MapPamEndpoints</c>) so the offline OpenAPI generator
    /// (<c>dotnet swagger tofile</c>) can discover them — it never runs the <c>Configure</c> pipeline where the
    /// endpoints are normally mapped. The discovery and swagger-only gating live in
    /// <see cref="EndpointDataSourceServiceCollectionExtensions.AddOpenApiEndpointDataSource"/>.
    /// </summary>
    private static IServiceCollection AddPamOpenApiEndpointDataSource(this IServiceCollection services)
        => services.AddOpenApiEndpointDataSource(endpoints => endpoints.MapPamEndpoints());
}
