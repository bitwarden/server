using System;
using Bit.Scim.Handlers;
using MediatR;

namespace Bit.Scim.Queries.Groups
{
    public record GetGroupQuery(Guid OrganizationId, Guid Id) : IRequest<RequestResult>;
}
