using Bit.AgentFill.Commands;
using Bit.AgentFill.Notifiers;
using Bit.AgentFill.Queries;
using Bit.AgentFill.Repositories;
using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.AgentFill;

/// <summary>Registration entry point for the AgentFill library.</summary>
public static class AgentFillServiceCollectionExtensions
{
    /// <summary>
    /// Registers the agent fill approval commands, query and repository, plus the feature flag keys the library owns.
    /// The repository is Dapper on SQL Server and Entity Framework otherwise, following
    /// <see cref="GlobalSettings.DatabaseProvider"/>. Hosts must already register <see cref="GlobalSettings"/>, the
    /// database (including <c>DatabaseContext</c> for non-SQL Server providers), <c>IPushNotificationService</c>,
    /// <c>ICurrentContext</c>, <c>IDeviceRepository</c> and <see cref="TimeProvider"/>.
    /// </summary>
    public static IServiceCollection AddAgentFill(this IServiceCollection services)
    {
        services.AddKnownFeatureFlags(AgentFillFeatureFlags.GetKeys());

        services.TryAddSingleton<DapperAgentFillApprovalRequestRepository>();
        services.TryAddSingleton<EntityFrameworkAgentFillApprovalRequestRepository>();
        services.TryAddSingleton<IAgentFillApprovalRequestRepository>(sp =>
            UsesDapper(sp.GetRequiredService<GlobalSettings>())
                ? sp.GetRequiredService<DapperAgentFillApprovalRequestRepository>()
                : sp.GetRequiredService<EntityFrameworkAgentFillApprovalRequestRepository>());

        services.TryAddScoped<IAgentFillRequestNotifier>(sp =>
        {
            // Development only: AgentFill:SimulatePushScript runs a local script in place of the APNS push.
            var script = sp.GetRequiredService<IConfiguration>()["AgentFill:SimulatePushScript"];
            return !string.IsNullOrWhiteSpace(script) && sp.GetRequiredService<IHostEnvironment>().IsDevelopment()
                ? new LocalScriptAgentFillRequestNotifier(script,
                    sp.GetRequiredService<ILogger<LocalScriptAgentFillRequestNotifier>>())
                : ActivatorUtilities.CreateInstance<PushAgentFillRequestNotifier>(sp);
        });
        services.TryAddScoped<ICreateApprovalRequestCommand, CreateApprovalRequestCommand>();
        services.TryAddScoped<IAnswerApprovalRequestCommand, AnswerApprovalRequestCommand>();
        services.TryAddScoped<IGetApprovalRequestQuery, GetApprovalRequestQuery>();
        services.TryAddScoped<IDeleteExpiredAgentFillApprovalRequestsCommand, DeleteExpiredAgentFillApprovalRequestsCommand>();

        return services;
    }

    /// <summary>Mirrors the host's provider selection: SQL Server, the default, uses Dapper.</summary>
    internal static bool UsesDapper(GlobalSettings globalSettings)
        => globalSettings.DatabaseProvider?.ToLowerInvariant() switch
        {
            "postgres" or "postgresql" or "mysql" or "mariadb" or "sqlite" => false,
            _ => true,
        };
}
