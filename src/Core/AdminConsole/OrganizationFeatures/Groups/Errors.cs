using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups;

public record CollectionNotFound : NotFoundError;

public record CannotModifyDefaultUserCollection()
    : BadRequestError("You cannot modify group access for collections with the type as DefaultUserCollection.");
