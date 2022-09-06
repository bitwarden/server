using MediatR;

namespace Bit.Scim.Commands.Groups;

public record DeleteGroupCommand(Guid OrganizationId, Guid Id) : IRequest;
