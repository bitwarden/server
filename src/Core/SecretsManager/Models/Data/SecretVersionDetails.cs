#nullable enable
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

/// <summary>
/// A secret version together with the display details of whoever archived it. Editor names are
/// carried separately rather than as entities because the two sources differ: member names are
/// stored in plaintext, while service account names are encrypted with the organization key and
/// can only be read by the client.
/// </summary>
public class SecretVersionDetails
{
    public required SecretVersion SecretVersion { get; set; }
    public string? EditorUserName { get; set; }
    public string? EditorUserEmail { get; set; }
    public string? EditorServiceAccountName { get; set; }
}
