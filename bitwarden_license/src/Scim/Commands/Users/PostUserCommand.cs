using Bit.Scim.Handlers;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Commands.Users
{
    public record PostUserCommand(Guid OrganizationId, ScimUserRequestModel Model) : IRequest<RequestResult>;
}
