namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface ICanEditRotationCiphersQuery
{
    /// <summary>
    /// Whether the current user can edit, and view the password of, every listed cipher in the organization. Rotation
    /// replaces the password and hands the cipher to an access connector, so managing it takes both.
    /// </summary>
    Task<bool> CanEditAllAsync(Guid organizationId, IEnumerable<Guid> cipherIds);
}
