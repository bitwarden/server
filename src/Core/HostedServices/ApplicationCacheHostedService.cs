using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.HostedServices;

public class ApplicationCacheHostedService : IHostedService, IDisposable
{
    private readonly InMemoryServiceBusApplicationCacheService _applicationCacheService;
    private readonly IOrganizationRepository _organizationRepository;
    protected readonly ILogger<ApplicationCacheHostedService> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusReceiver _subscriptionReceiver;
    private readonly ServiceBusAdministrationClient _serviceBusAdministrationClient;
    private readonly string _subName;
    private readonly string _topicName;
    private CancellationTokenSource _cts;
    private Task _executingTask;


    public ApplicationCacheHostedService(
        IApplicationCacheService applicationCacheService,
        IOrganizationRepository organizationRepository,
        ILogger<ApplicationCacheHostedService> logger,
        GlobalSettings globalSettings)
    {
        _topicName = globalSettings.ServiceBus.ApplicationCacheTopicName;
        _subName = CoreHelpers.GetApplicationCacheServiceBusSubscriptionName(globalSettings);
        _applicationCacheService = applicationCacheService as InMemoryServiceBusApplicationCacheService;
        _organizationRepository = organizationRepository;
        _logger = logger;
        _serviceBusClient = new ServiceBusClient(globalSettings.ServiceBus.ConnectionString);
        _subscriptionReceiver = _serviceBusClient.CreateReceiver(_topicName, _subName);
        _serviceBusAdministrationClient = new ServiceBusAdministrationClient(globalSettings.ServiceBus.ConnectionString);
    }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _serviceBusAdministrationClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(_topicName, _subName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromDays(14),
                LockDuration = TimeSpan.FromSeconds(30),
                EnableDeadLetteringOnFilterEvaluationExceptions = true,
                DeadLetteringOnMessageExpiration = true,
            }, new CreateRuleOptions
            {
                Filter = new SqlRuleFilter($"sys.label != '{_subName}'")
            }, cancellationToken);
        }
        catch (ServiceBusException e)
        when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        { }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = ExecuteAsync(_cts.Token);
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        await _subscriptionReceiver.CloseAsync(cancellationToken);
        await _serviceBusClient.DisposeAsync();
        _cts.Cancel();
        try
        {
            await _serviceBusAdministrationClient.DeleteSubscriptionAsync(_topicName, _subName, cancellationToken);
        }
        catch { }
        await _executingTask;
    }

    public virtual void Dispose()
    { }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _subscriptionReceiver.ReceiveMessagesAsync(cancellationToken))
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing messages in ApplicationCacheHostedService");
            }
        }
    }

    private async Task ProcessMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        if (message.Subject != _subName && _applicationCacheService != null)
        {
            switch ((ApplicationCacheMessageType)message.ApplicationProperties["type"])
            {
                case ApplicationCacheMessageType.UpsertOrganizationAbility:
                    var upsertedOrgId = (Guid)message.ApplicationProperties["id"];
                    var upsertedOrg = await _organizationRepository.GetByIdAsync(upsertedOrgId);
                    if (upsertedOrg != null)
                    {
                        await _applicationCacheService.BaseUpsertOrganizationAbilityAsync(upsertedOrg);
                    }
                    break;
                case ApplicationCacheMessageType.DeleteOrganizationAbility:
                    await _applicationCacheService.BaseDeleteOrganizationAbilityAsync(
                        (Guid)message.ApplicationProperties["id"]);
                    break;
                default:
                    break;
            }
        }
        if (!cancellationToken.IsCancellationRequested)
        {
            await _subscriptionReceiver.CompleteMessageAsync(message, cancellationToken);
        }
    }
}
