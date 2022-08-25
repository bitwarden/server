using Bit.Scim.Handlers;
using MediatR;

namespace Bit.Scim.Queries.Users
{
    public record GetUsersListQuery(Guid OrganizationId, string Filter, int? Count, int? StartIndex) : IRequest<RequestResult>;
}
