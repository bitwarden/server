using System;
using Bit.Scim.Handlers;
using MediatR;

namespace Bit.Scim.Queries.Groups
{
    public record GetGroupsListQuery(Guid OrganizationId, string Filter, int? Count, int? StartIndex) : IRequest<RequestResult>;
}
