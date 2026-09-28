using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetAccessRequestDetailsQuery
{
    /// <summary>
    /// Returns one access request's details. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> if it
    /// doesn't exist or the caller is neither its requester nor a managing approver of its collection.
    /// </summary>
    /// <param name="now">The caller's read clock.</param>
    Task<AccessRequestDetails> GetDetailsAsync(Guid userId, Guid requestId, DateTime now);
}
