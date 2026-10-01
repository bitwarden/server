namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRevokeAccessLeaseCommand
{
    /// <summary>
    /// Ends an active lease early. The caller must be either the lease's holder or able to Manage the lease's
    /// collection. A holder ending their own lease settles it to cancelled; a collection manager ending it settles it
    /// to revoked. The actor is recorded as the revoker.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The lease does not exist, or the caller is neither its holder nor able to Manage its collection.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">The lease is not active.</exception>
    Task RevokeAsync(Guid userId, Guid leaseId, string? reason);
}
