using Bit.Core.Pam.Services;
using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Services.Pam.Utilities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPamServices(this IServiceCollection services)
    {
        // Minimal API endpoint handlers. The endpoints (see PamEndpointsExtensions) resolve these from DI.
        services.AddScoped<LeaseEndpointsHandler>();
        services.AddScoped<AccessRequestEndpointsHandler>();
        services.AddScoped<AccessRuleEndpointsHandler>();
        services.AddScoped<CipherLeaseEndpointsHandler>();
        services.AddScoped<AccessConnectorEndpointsHandler>();
        services.AddScoped<TargetSystemEndpointsHandler>();
        services.AddScoped<RotationConfigEndpointsHandler>();
        services.AddScoped<RotationJobEndpointsHandler>();
        services.AddScoped<RotationAttemptEndpointsHandler>();

        // The read decision point Vault code consults before releasing a cipher's secrets. AddBaseServices
        // registers the open-source UnrestrictedCipherLeaseGate, which gates nothing; this overrides it by
        // last-one-wins, which holds because Startup calls AddPamServices after AddBaseServices and both
        // registrations are a plain Add. A TryAdd on either side would silently leave leasing ungated, so
        // keep this an AddScoped — CipherLeaseGateRegistrationTests pins both halves of that contract.
        services.AddScoped<ICipherLeaseGate, CipherLeaseGate>();

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

        // Read models behind the approver inbox, the caller's own requests, the lease surfaces, and the per-cipher
        // pre-check and access-state snapshot.
        services.AddScoped<IAccessPreCheckQuery, AccessPreCheckQuery>();
        services.AddScoped<IGetCipherAccessStateQuery, GetCipherAccessStateQuery>();
        services.AddScoped<IGetAccessRequestDetailsQuery, GetAccessRequestDetailsQuery>();
        services.AddScoped<IListInboxRequestsQuery, ListInboxRequestsQuery>();
        services.AddScoped<IListInboxHistoryQuery, ListInboxHistoryQuery>();
        services.AddScoped<IListMyAccessRequestsQuery, ListMyAccessRequestsQuery>();
        services.AddScoped<IListActiveLeasesQuery, ListActiveLeasesQuery>();
        services.AddScoped<IListLeaseHistoryQuery, ListLeaseHistoryQuery>();
        services.AddScoped<IListMyActiveAccessLeasesQuery, ListMyActiveAccessLeasesQuery>();

        // Access-request and lease write path.
        services.AddScoped<ISubmitAccessRequestCommand, SubmitAccessRequestCommand>();
        services.AddScoped<IDecideAccessRequestCommand, DecideAccessRequestCommand>();
        services.AddScoped<IActivateAccessRequestCommand, ActivateAccessRequestCommand>();
        services.AddScoped<ICancelAccessRequestCommand, CancelAccessRequestCommand>();
        services.AddScoped<IRequestLeaseExtensionCommand, RequestLeaseExtensionCommand>();
        services.AddScoped<IRevokeAccessLeaseCommand, RevokeAccessLeaseCommand>();

        // Supporting reads for the write path: who may approve for a collection, and the per-cipher
        // single-active-lease guard applied at activation.
        services.AddScoped<IApproverCollectionAccessQuery, ApproverCollectionAccessQuery>();
        services.AddScoped<ISingleActiveLeaseEvaluator, SingleActiveLeaseEvaluator>();

        // Side channels the commands emit through. The two notifiers send the RefreshApproverInbox and
        // RefreshAccessRequest pushes; the audit emitter stays inert because the audit store it would write to is
        // separate work. Registering them is not optional — every command above takes all three, so dropping one
        // turns each PAM request into a DI resolution failure at runtime rather than a compile error.
        services.AddScoped<IApproverInboxNotifier, ApproverInboxNotifier>();
        services.AddScoped<IRequesterNotifier, RequesterNotifier>();
        services.AddScoped<IAccessAuditEventEmitter, NoopAccessAuditEventEmitter>();

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
