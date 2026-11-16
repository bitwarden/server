namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRevokeAccessLeaseCommand
{
    /// <summary>
    /// Ends an active lease early: cancelled by its holder, or revoked by someone who can Manage its collection.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The lease does not exist, or the caller is neither its holder nor able to Manage its collection.
    /// </exception>
    Task RevokeAsync(Guid userId, Guid leaseId, string? reason);
}
