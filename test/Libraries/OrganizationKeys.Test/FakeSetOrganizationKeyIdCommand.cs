using Bit.Core.KeyManagement.Models.Data;
using Bit.OrganizationKeys.Commands;

namespace Bit.OrganizationKeys.Test;

/// <summary>Records the call so tests can assert on it, and throws <see cref="Exception"/> when set.</summary>
internal sealed class FakeSetOrganizationKeyIdCommand : ISetOrganizationKeyIdCommand
{
    public Exception? Exception { get; set; }
    public bool Called { get; private set; }
    public Guid ReceivedOrganizationId { get; private set; }
    public KeyId? ReceivedOrganizationKeyId { get; private set; }

    public Task SetOrganizationKeyIdAsync(Guid organizationId, KeyId organizationKeyId)
    {
        Called = true;
        ReceivedOrganizationId = organizationId;
        ReceivedOrganizationKeyId = organizationKeyId;
        return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
    }
}
