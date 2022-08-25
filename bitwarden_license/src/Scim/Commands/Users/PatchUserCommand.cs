using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Commands.Users
{
    public record PatchUserCommand(Guid OrganizationId, Guid Id, ScimPatchModel Model) : IRequest;
}
