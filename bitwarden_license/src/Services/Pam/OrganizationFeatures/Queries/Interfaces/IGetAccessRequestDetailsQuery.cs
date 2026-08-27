using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetAccessRequestDetailsQuery
{
    /// <summary>
    /// Returns one access request's full details (the same projection the list endpoints return) for the dedicated
    /// request page. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> when no request has the id, or the
    /// caller is neither its requester nor a managing approver of its collection — so a caller can't probe for requests
    /// they have no business seeing. Unlike the decide surface this is a read and does NOT block the requester from
    /// viewing their own request.
    /// </summary>
    /// <param name="now">The caller's read clock: it filters/windows the read where applicable, and the derived
    /// statuses stamped on the returned details are computed against the same instant.</param>
    Task<AccessRequestDetails> GetDetailsAsync(Guid userId, Guid requestId, DateTime now);
}
