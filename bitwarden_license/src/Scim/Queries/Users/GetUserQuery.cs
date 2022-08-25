using Bit.Scim.Handlers;
using MediatR;

namespace Bit.Scim.Queries.Users
{
    public record GetUserQuery(Guid OrganizationId, Guid Id) : IRequest<RequestResult>;
}
