using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Queries.Groups;

public record GetGroupsListQuery(Guid OrganizationId, string Filter, int? Count, int? StartIndex) : IRequest<ScimListResponseModel<ScimGroupResponseModel>>;
