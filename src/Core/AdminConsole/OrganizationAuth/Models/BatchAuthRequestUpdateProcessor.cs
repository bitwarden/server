using Bit.Core.Auth.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class BatchAuthRequestUpdateProcessor
{
    public List<AuthRequestUpdateProcessor> Processors { get; } = new List<AuthRequestUpdateProcessor>();
    private List<AuthRequestUpdateProcessor> _processed => Processors
        .Where(p => p.ProcessedAuthRequest != null)
        .ToList();

    public BatchAuthRequestUpdateProcessor(
        ICollection<OrganizationAdminAuthRequest> authRequests,
        IEnumerable<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration
    )
    {
        Processors = authRequests?.Select(ar =>
        {
            return new AuthRequestUpdateProcessor(
                ar,
                updates.FirstOrDefault(u => u.Id == ar.Id),
                configuration
            );
        }).ToList() ?? Processors;
    }

    public BatchAuthRequestUpdateProcessor Process(Action<Exception> errorHandlerCallback)
    {
        foreach (var processor in Processors)
        {
            try
            {
                processor.Process();
            }
            catch (AuthRequestUpdateProcessingException e)
            {
                errorHandlerCallback(e);
            }
        }
        return this;
    }

    public async Task Save(Func<IEnumerable<OrganizationAdminAuthRequest>, Task> callback)
    {
        if (_processed.Any())
        {
            await callback(_processed.Select(p => p.ProcessedAuthRequest));
        }
    }

    // Currently push notifications and emails are still done per-request in
    // a loop, which is different than saving updates to the database and
    // raising organization events. These can be done in bulk all the way
    // through to the repository.
    //
    // Adding bulk notification and email methods is being tracked as tech
    // debt on https://bitwarden.atlassian.net/browse/AC-2629
    public async Task SendPushNotifications(Func<OrganizationAdminAuthRequest, Task> callback)
    {
        foreach (var processor in _processed)
        {
            await processor.SendPushNotification(callback);
        }
    }

    public async Task SendApprovalEmailsForProcessedRequests(Func<OrganizationAdminAuthRequest, string, Task> callback)
    {
        foreach (var processor in _processed)
        {
            await processor.SendApprovalEmail(callback);
        }
    }

    public async Task LogOrganizationEventsForProcessedRequests(Func<IEnumerable<(OrganizationAdminAuthRequest, EventType)>, Task> callback)
    {
        if (_processed.Any())
        {
            await callback(_processed.Select(p =>
            {
                return (p.ProcessedAuthRequest, p.OrganizationEventType);
            }));
        }
    }
}
