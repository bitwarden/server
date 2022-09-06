using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Commands.Groups;

public record PatchGroupCommand(Guid OrganizationId, Guid Id, ScimPatchModel Model) : IRequest;
