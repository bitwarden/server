using Bit.Scim.Handlers;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Commands.Users
{
    public record PutUserCommand(Guid OrganizationId, Guid Id, ScimUserRequestModel Model) : IRequest<RequestResult>;
}
