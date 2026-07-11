using Bit.HttpExtensions;
using Bit.Pam.Services;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Rotation;
using Bit.Services.Pam.Rotation.Api.Endpoints.Filters;
using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Commands;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Services.Pam.Rotation.Queries;
using Bit.Services.Pam.Rotation.Queries.Interfaces;
using Bit.Services.Pam.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Services.Pam.Utilities;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PAM's commercial services, including credential rotation. <paramref name="configuration"/> binds
    /// <see cref="PamRotationOptions"/> from <c>globalSettings:pam:rotation</c> -- AddPamServices previously took no
    /// configuration, so this is a new parameter on an existing call site (see <c>Startup.ConfigureServices</c>)
    /// rather than a pre-existing pattern in this file.
    /// </summary>
    public static IServiceCollection AddPamServices(this IServiceCollection services, IConfiguration configuration)
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
        services.AddScoped<AuditEndpointsHandler>();
        services.AddScoped<AccessRequestEndpointsHandler>();
        services.AddScoped<AccessRuleEndpointsHandler>();
        services.AddScoped<CipherLeaseEndpointsHandler>();

        // Credential rotation's Minimal API endpoint handlers (see Rotation/Api/Endpoints).
        services.AddScoped<RotationDaemonEndpointsHandler>();
        services.AddScoped<RotationTargetSystemEndpointsHandler>();
        services.AddScoped<RotationConfigEndpointsHandler>();
        services.AddScoped<RotationDaemonJobsEndpointsHandler>();
        services.AddScoped<RotationJobEndpointsHandler>();
        services.AddScoped<RotationAttemptEndpointsHandler>();

        // Runs on every daemon-facing rotation route (see PamEndpointsExtensions.WithPamDaemonDefaults). Registered
        // explicitly even though its parameterless constructor would let AddEndpointFilter<T>() construct it
        // unregistered (as PamExceptionHandlerEndpointFilter/PamValidationEndpointFilter already do) -- being
        // explicit here documents that it participates in the request pipeline, since unlike those two it resolves
        // several other services from the request's provider inside InvokeAsync.
        services.AddScoped<DaemonRequestEndpointFilter>();

        services.AddPamRotationServices(configuration);
        services.AddPamOpenApiEndpointDataSource();

        return services;
    }

    /// <summary>
    /// Registers PAM credential rotation: the schedule calculator, the admin/dispatch commands, and the read
    /// queries under <c>Rotation/</c>. Options are bound from <c>globalSettings:pam:rotation</c> (see
    /// <see cref="PamRotationOptions"/> for defaults); the Quartz sweep jobs and Dapper repositories are registered
    /// elsewhere (commercial job host / <c>DapperServiceCollectionExtensions</c>).
    /// </summary>
    private static IServiceCollection AddPamRotationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PamRotationOptions>(configuration.GetSection("globalSettings:pam:rotation"));

        // Stateless and cheap to construct; shared across the process like IAccessRuleEngine.
        services.AddSingleton<IRotationScheduleCalculator, RotationScheduleCalculator>();

        // Admin commands
        services.AddScoped<IRegisterDaemonCommand, RegisterDaemonCommand>();
        services.AddScoped<ISetDaemonStatusCommand, SetDaemonStatusCommand>();
        services.AddScoped<IDeleteDaemonCommand, DeleteDaemonCommand>();
        services.AddScoped<IAssignDaemonToTargetCommand, AssignDaemonToTargetCommand>();
        services.AddScoped<IUnassignDaemonFromTargetCommand, UnassignDaemonFromTargetCommand>();
        services.AddScoped<IRegisterTargetSystemCommand, RegisterTargetSystemCommand>();
        services.AddScoped<ISetTargetSystemStatusCommand, SetTargetSystemStatusCommand>();
        services.AddScoped<IRenameTargetSystemCommand, RenameTargetSystemCommand>();
        services.AddScoped<IUpdateTargetSystemPolicyCommand, UpdateTargetSystemPolicyCommand>();
        services.AddScoped<ICreateRotationConfigCommand, CreateRotationConfigCommand>();
        services.AddScoped<IUpdateRotationSettingsCommand, UpdateRotationSettingsCommand>();
        services.AddScoped<IUpdateRotationAccountCommand, UpdateRotationAccountCommand>();
        services.AddScoped<IPauseRotationCommand, PauseRotationCommand>();
        services.AddScoped<IResumeRotationCommand, ResumeRotationCommand>();
        services.AddScoped<IDeleteRotationConfigCommand, DeleteRotationConfigCommand>();
        services.AddScoped<ITriggerRotationCommand, TriggerRotationCommand>();
        services.AddScoped<IRecordManualRotationCommand, RecordManualRotationCommand>();

        // Dispatch / daemon commands
        services.AddScoped<IOfferRotationCommand, OfferRotationCommand>();
        services.AddScoped<IHandleAccessGrantEndedCommand, HandleAccessGrantEndedCommand>();
        services.AddScoped<IClaimRotationJobCommand, ClaimRotationJobCommand>();
        services.AddScoped<IReportRotationSucceededCommand, ReportRotationSucceededCommand>();
        services.AddScoped<IReportRotationFailedCommand, ReportRotationFailedCommand>();
        services.AddScoped<ISubmitCipherUpdateCommand, SubmitCipherUpdateCommand>();

        // Read queries
        services.AddScoped<IListRotationConfigsQuery, ListRotationConfigsQuery>();
        services.AddScoped<IGetRotationConfigDetailsQuery, GetRotationConfigDetailsQuery>();
        services.AddScoped<IListDaemonsQuery, ListDaemonsQuery>();
        services.AddScoped<IListTargetSystemsQuery, ListTargetSystemsQuery>();
        services.AddScoped<IListClaimableJobsQuery, ListClaimableJobsQuery>();
        services.AddScoped<IGetRotationCipherQuery, GetRotationCipherQuery>();

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
