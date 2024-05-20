using Bit.Core.Auth.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class BatchAuthRequestUpdateProcessor<T> where T : AuthRequest
{
    public List<AuthRequestUpdateProcessor<T>> Processors { get; } = new List<AuthRequestUpdateProcessor<T>>();
    private List<AuthRequestUpdateProcessor<T>> _processed => Processors
        .Where(p => p.ProcessedAuthRequest != null)
        .ToList();

    public BatchAuthRequestUpdateProcessor(
        ICollection<T> authRequests,
        IEnumerable<OrganizationAuthRequestUpdate> updates,
        AuthRequestUpdateProcessorConfiguration configuration
    )
    {
        Processors = authRequests?.Select(ar =>
        {
            return new AuthRequestUpdateProcessor<T>(
                ar,
                updates.FirstOrDefault(u => u.Id == ar.Id),
                configuration
            );
        }).ToList() ?? Processors;
    }

    public BatchAuthRequestUpdateProcessor<T> Process(Action<Exception> errorHandlerCallback)
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

    public async Task<BatchAuthRequestUpdateProcessor<T>> Save(Func<IEnumerable<T>, Task> callback)
    {
        if (_processed.Any())
        {
            await callback(_processed.Select(p => p.ProcessedAuthRequest));
        }
        return this;
    }

    // Currently push notifications and emails are still done per-request in
    // a loop, which is different than saving updates to the database and
    // raising organization events. These can be done in bulk all the way
    // through to the repository.
    //
    // Adding bulk notification and email methods is being tracked as tech
    // debt on https://bitwarden.atlassian.net/browse/AC-2629
    public async Task<BatchAuthRequestUpdateProcessor<T>> SendPushNotifications(Func<T, Task> callback)
    {
        foreach (var processor in _processed)
        {
            await processor.SendPushNotification(callback);
        }
        return this;
    }

    public async Task<BatchAuthRequestUpdateProcessor<T>> SendNewDeviceEmails(Func<T, string, Task> callback)
    {
        foreach (var processor in _processed)
        {
            await processor.SendNewDeviceEmail(callback);
        }
        return this;
    }

    public async Task<BatchAuthRequestUpdateProcessor<T>> SendEventLogs(Func<IEnumerable<(T, EventType)>, Task> callback)
    {
        if (_processed.Any())
        {
            await callback(_processed.Select(p =>
            {
                return (p.ProcessedAuthRequest, p.OrganizationEventType);
            }));
        }
        return this;
    }
}
