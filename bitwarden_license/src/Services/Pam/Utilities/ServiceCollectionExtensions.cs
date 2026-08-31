using Bit.Core.Pam.Services;
using Bit.HttpExtensions;
using Bit.Services.Pam.AccessConnector;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Filters;
using Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Commands;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Queries;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Services.Pam.Utilities;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PAM's commercial services, including credential rotation. <paramref name="configuration"/> binds
    /// <see cref="PamRotationOptions"/> from <c>globalSettings:pam:rotation</c>.
    /// </summary>
    public static IServiceCollection AddPamServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<LeaseEndpointsHandler>();
        services.AddScoped<AccessRequestEndpointsHandler>();
        services.AddScoped<AccessRuleEndpointsHandler>();
        services.AddScoped<CipherLeaseEndpointsHandler>();
        services.AddScoped<AuditEndpointsHandler>();
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

        services.AddScoped<IGoverningRuleResolver, GoverningRuleResolver>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAccessRuleValidator, AccessRuleValidator>();
        services.AddScoped<IAccessRuleWriteValidator, AccessRuleWriteValidator>();
        services.AddScoped<ICreateAccessRuleCommand, CreateAccessRuleCommand>();
        services.AddScoped<IUpdateAccessRuleCommand, UpdateAccessRuleCommand>();
        services.AddScoped<IDeleteAccessRuleCommand, DeleteAccessRuleCommand>();

        services.AddScoped<IAccessPreCheckQuery, AccessPreCheckQuery>();
        services.AddScoped<IGetCipherAccessStateQuery, GetCipherAccessStateQuery>();
        services.AddScoped<IGetAccessRequestDetailsQuery, GetAccessRequestDetailsQuery>();
        services.AddScoped<IListInboxRequestsQuery, ListInboxRequestsQuery>();
        services.AddScoped<IListInboxHistoryQuery, ListInboxHistoryQuery>();
        services.AddScoped<IListMyAccessRequestsQuery, ListMyAccessRequestsQuery>();
        services.AddScoped<IListActiveLeasesQuery, ListActiveLeasesQuery>();
        services.AddScoped<IListLeaseHistoryQuery, ListLeaseHistoryQuery>();
        services.AddScoped<IListAccessAuditTrailQuery, ListAccessAuditTrailQuery>();
        services.AddScoped<IListRuleBypassableCiphersQuery, ListRuleBypassableCiphersQuery>();

        services.AddScoped<ISubmitAccessRequestCommand, SubmitAccessRequestCommand>();
        services.AddScoped<IDecideAccessRequestCommand, DecideAccessRequestCommand>();
        services.AddScoped<IActivateAccessRequestCommand, ActivateAccessRequestCommand>();
        services.AddScoped<ICancelAccessRequestCommand, CancelAccessRequestCommand>();
        services.AddScoped<IRequestLeaseExtensionCommand, RequestLeaseExtensionCommand>();
        services.AddScoped<IRevokeAccessLeaseCommand, RevokeAccessLeaseCommand>();

        services.AddScoped<IApproverCollectionAccessQuery, ApproverCollectionAccessQuery>();
        services.AddScoped<ISingleActiveLeaseEvaluator, SingleActiveLeaseEvaluator>();

        // Side channels the commands emit through. The two notifiers send the RefreshApproverInbox and
        // RefreshAccessRequest pushes; the audit emitter appends to the PAM audit store. Registering them is not
        // optional — every command above takes all three, so dropping one turns each PAM request into a DI
        // resolution failure at runtime rather than a compile error.
        services.AddScoped<IApproverInboxNotifier, ApproverInboxNotifier>();
        services.AddScoped<IRequesterNotifier, RequesterNotifier>();
        services.AddScoped<IAccessAuditEventEmitter, AccessAuditEventEmitter>();

        services.TryAddScoped<IAccessMailNotifier, AccessMailNotifier>();
        services.TryAddScoped<IApproverMailNotifier, ApproverMailNotifier>();

        // Runs on every connector-facing route (see PamEndpointsExtensions.WithPamAccessConnectorMachineDefaults).
        // Its
        // parameterless constructor would let AddEndpointFilter<T>() construct it unregistered, as
        // PamExceptionHandlerEndpointFilter/PamValidationEndpointFilter are -- registering it anyway keeps a filter
        // that resolves services of its own visible in the container.
        services.AddScoped<AccessConnectorHeartbeatEndpointFilter>();

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

        services.AddScoped<IRegisterAccessConnectorCommand, RegisterAccessConnectorCommand>();
        services.AddScoped<ISetAccessConnectorStatusCommand, SetAccessConnectorStatusCommand>();
        services.AddScoped<IDeleteAccessConnectorCommand, DeleteAccessConnectorCommand>();
        services.AddScoped<IAssignAccessConnectorToTargetCommand, AssignAccessConnectorToTargetCommand>();
        services.AddScoped<IUnassignAccessConnectorFromTargetCommand, UnassignAccessConnectorFromTargetCommand>();
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

        services.AddScoped<IOfferRotationCommand, OfferRotationCommand>();
        services.AddScoped<IHandleAccessGrantEndedCommand, HandleAccessGrantEndedCommand>();
        services.AddScoped<IClaimRotationJobCommand, ClaimRotationJobCommand>();
        services.AddScoped<IReportRotationSucceededCommand, ReportRotationSucceededCommand>();
        services.AddScoped<IReportRotationFailedCommand, ReportRotationFailedCommand>();
        services.AddScoped<ISubmitCipherUpdateCommand, SubmitCipherUpdateCommand>();

        services.AddScoped<IGetRotationConfigDetailsQuery, GetRotationConfigDetailsQuery>();
        services.AddScoped<IListAccessConnectorsQuery, ListAccessConnectorsQuery>();
        services.AddScoped<IGetAccessConnectorDetailsQuery, GetAccessConnectorDetailsQuery>();
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
