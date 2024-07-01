#nullable enable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Tools.Entities;

/// <summary>
/// An end-to-end encrypted secret accessible to arbitrary
/// entities through a fixed URI.
/// </summary>
public class Send : ITableObject<Guid>
{
    /// <summary>
    /// Uniquely identifies this send.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifies the user that created this send.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Identifies the organization that created this send.
    /// </summary>
    /// <remarks>
    /// Not presently in-use by client applications.
    /// </remarks>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Describes the data being sent. This field determines how
    /// the <see cref="Data"/> field is interpreted.
    /// </summary>
    public SendType Type { get; set; }

    /// <summary>
    /// Stores data containing or pointing to the transmitted secret. JSON.
    /// </summary>
    /// <note>
    /// Must be nullable due to several database column configuration.
    /// The application and all other databases assume this is not nullable.
    /// Tech debt ticket: PM-4128
    /// </note>
    public string? Data { get; set; }

    /// <summary>
    /// Stores the data's encryption key. Encrypted.
    /// </summary>
    /// <note>
    /// Must be nullable due to MySql database column configuration.
    /// The application and all other databases assume this is not nullable.
    /// Tech debt ticket: PM-4128
    /// </note>
    public string? Key { get; set; }

    /// <summary>
    /// Password provided by the user. Protected with pbkdf2.
    /// </summary>
    [MaxLength(300)]
    public string? Password { get; set; }

    /// <summary>
    /// The send becomes unavailable to API callers when
    /// <see cref="AccessCount"/>  &gt;= <see cref="MaxAccessCount"/>.
    /// </summary>
    public int? MaxAccessCount { get; set; }

    /// <summary>
    /// Number of times the content was accessed.
    /// </summary>
    /// <remarks>
    /// This value is owned by the server. Clients cannot alter it.
    /// </remarks>
    public int AccessCount { get; set; }

    /// <summary>
    /// The date this send was created.
    /// </summary>
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// The date this send was last modified.
    /// </summary>
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// The date this send becomes unavailable to API callers.
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// The date this send will be unconditionally deleted.
    /// </summary>
    /// <remarks>
    /// This is set by server-side when the user doesn't specify a deletion date.
    /// </remarks>
    public DateTime DeletionDate { get; set; }

    /// <summary>
    /// When this is true the send is not available to API callers,
    /// unless they're the creator.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Whether the creator's email address should be shown to the recipient.
    /// </summary>
    /// <value>
    /// <see langword="false"/> indicates the email may be shown.
    /// <see langword="true"/> indicates the email should be hidden.
    /// <see langword="null"/> indicates the client doesn't set the field and
    /// the email should be hidden.
    /// </value>
    public bool? HideEmail { get; set; }

    /// <summary>
    /// Identifies the Cipher associated with this send.
    /// </summary>
    public Guid? CipherId { get; set; }

    /// <summary>
    /// Generates the send's <see cref="Id" />
    /// </summary>
    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
