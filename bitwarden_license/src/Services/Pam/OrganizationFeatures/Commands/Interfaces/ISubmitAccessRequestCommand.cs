using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ISubmitAccessRequestCommand
{
    /// <summary>
    /// Submits an access request for a leasing-gated cipher and auto-approves it when the governing rule carries no
    /// human-approval gate, returning the created (Approved) request. The requester then activates it
    /// (<see cref="IActivateAccessRequestCommand"/>) to mint the lease. Human-approval rules are not supported in this
    /// build — the approver-inbox path is deferred.
    /// </summary>
    Task<AccessRequest> SubmitAsync(Guid userId, Guid cipherId, int durationSeconds, string? reason);
}
