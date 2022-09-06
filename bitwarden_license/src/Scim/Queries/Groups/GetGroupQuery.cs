using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Queries.Groups;

public record GetGroupQuery(Guid OrganizationId, Guid Id) : IRequest<ScimGroupResponseModel>;
