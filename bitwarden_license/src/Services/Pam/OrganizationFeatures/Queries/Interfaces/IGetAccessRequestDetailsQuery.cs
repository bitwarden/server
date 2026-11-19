using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetAccessRequestDetailsQuery
{
    /// <summary>
    /// Returns one access request's details. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> unless the
    /// caller is its requester or a managing approver of its collection.
    /// </summary>
    Task<AccessRequestDetails> GetDetailsAsync(Guid userId, Guid requestId, DateTime now);
}
