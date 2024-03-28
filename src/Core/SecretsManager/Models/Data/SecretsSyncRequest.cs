#nullable enable
using Bit.Core.Enums;

namespace Bit.Core.SecretsManager.Models.Data;

public class SecretsSyncRequest
{
    public AccessClientType AccessClientType { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ServiceAccountId { get; set; }
    public DateTime? LastSyncedDate { get; set; }
}
