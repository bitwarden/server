using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Commands.Users
{
    public record DeleteUserCommand(Guid OrganizationId, Guid Id, ScimUserRequestModel Model) : IRequest;
}
