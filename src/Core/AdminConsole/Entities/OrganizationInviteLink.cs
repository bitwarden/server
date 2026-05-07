using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// A shareable link that allows users to join an organization without a per-email address invitation.
/// </summary>
public class OrganizationInviteLink : ITableObject<Guid>
{
    /// <summary>
    /// A unique identifier for the invite link.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// A random, secret code embedded in the invite link to ensure it cannot be guessed.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Guid.NewGuid"/> rather than a sequential/comb GUID because this is not
    /// a table identifier and therefore does not need index-friendly ordering. A comb GUID's embedded
    /// timestamp would also make the code partially predictable.
    /// </remarks>
    public Guid Code { get; set; } = Guid.NewGuid();
    /// <summary>
    /// The ID of the <see cref="Organization"/> this invite link belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }
    /// <summary>
    /// A JSON-serialized list of email domains that are permitted to use this invite link.
    /// Use <see cref="GetAllowedDomains"/> and <see cref="SetAllowedDomains"/> to read and write this field.
    /// </summary>
    public string AllowedDomains { get; set; } = null!;
    /// <summary>
    /// The invite link key encrypted with the organization's symmetric key.
    /// </summary>
    /// <remarks>
    /// This is decrypted client-side and used to reconstruct the invite link for display to the admin.
    /// </remarks>
    public string EncryptedInviteKey { get; set; } = null!;
    /// <summary>
    /// The organization's symmetric key, encrypted with the invite link key.
    /// </summary>
    /// <remarks>
    /// This is used to support automatic confirmation of invited users by allowing a user who clicks the link
    /// to decrypt a copy of the organization symmetric key.
    /// </remarks>
    public string? EncryptedOrgKey { get; set; }
    /// <summary>
    /// The date the invite link was created.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the invite link was last updated.
    /// </summary>
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deserializes <see cref="AllowedDomains"/> into a list of domain strings.
    /// </summary>
    public IEnumerable<string> GetAllowedDomains() =>
        JsonSerializer.Deserialize<IEnumerable<string>>(AllowedDomains) ?? [];

    /// <summary>
    /// Serializes the given domains and stores them in <see cref="AllowedDomains"/>.
    /// </summary>
    public void SetAllowedDomains(IEnumerable<string> domains) =>
        AllowedDomains = JsonSerializer.Serialize(domains);

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
