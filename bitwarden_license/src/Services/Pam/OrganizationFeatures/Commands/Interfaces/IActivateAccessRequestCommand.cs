using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IActivateAccessRequestCommand
{
    /// <summary>
    /// Activates an approved access request, minting its short-lived lease. Idempotent while the produced lease is
    /// live. Throws when the request isn't the caller's, isn't approved, its window is closed, or a lost race /
    /// single-active-lease conflict prevents the mint.
    /// </summary>
    Task<AccessLease> ActivateAsync(Guid userId, Guid requestId);
}
