using Bit.Core.Entities;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Commands.Groups;

public record PostGroupCommand(Guid OrganizationId, ScimGroupRequestModel Model) : IRequest<Group>;
