using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;

public record PrivilegedControlsSeatLimitReached() : BadRequestError("Privileged Controls seat limit has been reached. Ask an Owner to add seats or raise the limit.");
