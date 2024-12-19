namespace Bit.Core.Vault.Models.Data;

/// <summary>
/// Data model that represents a Users permissions for a given cipher
/// that belongs to an organization.
/// To be used internally for authorization.
/// </summary>
public class OrganizationCipherPermission
{
    /// <summary>
    /// The cipher Id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The organization Id that the cipher belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The user can read the cipher.
    /// See <see cref="ViewPassword"/> for password visibility.
    /// </summary>
    public bool Read { get; set; }

    /// <summary>
    /// The user has permission to view the password of the cipher.
    /// </summary>
    public bool ViewPassword { get; set; }

    /// <summary>
    /// The user has permission to edit the cipher.
    /// </summary>
    public bool Edit { get; set; }

    /// <summary>
    /// The user has manage level access to the cipher.
    /// </summary>
    public bool Manage { get; set; }

    /// <summary>
    /// The cipher is not assigned to any collection within the organization.
    /// </summary>
    public bool Unassigned { get; set; }
}
