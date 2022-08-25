using Bit.Scim.Handlers;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Commands.Groups
{
    public record PutGroupCommand(Guid OrganizationId, Guid Id, ScimGroupRequestModel Model) : IRequest<RequestResult>;
}
