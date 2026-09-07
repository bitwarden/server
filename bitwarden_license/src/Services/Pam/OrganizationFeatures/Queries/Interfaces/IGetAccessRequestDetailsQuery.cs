using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetAccessRequestDetailsQuery
{
    /// <summary>
    /// Returns one access request's full details for the dedicated request page. Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> if no request has the id, or the caller is neither its
    /// requester nor a managing approver of its collection. Unlike the decide surface, this does not block the
    /// requester from viewing their own request.
    /// </summary>
    /// <param name="now">The caller's read clock, for windowing and for the derived statuses stamped on the result.</param>
    Task<AccessRequestDetails> GetDetailsAsync(Guid userId, Guid requestId, DateTime now);
}
