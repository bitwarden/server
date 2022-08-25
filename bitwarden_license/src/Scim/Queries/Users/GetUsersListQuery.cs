using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Queries.Users
{
    public record GetUsersListQuery(Guid OrganizationId, string Filter, int? Count, int? StartIndex) : IRequest<ScimListResponseModel<ScimUserResponseModel>>;
}
