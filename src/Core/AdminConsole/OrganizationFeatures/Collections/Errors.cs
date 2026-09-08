using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections;

public record CollectionAccessInvalidError() : BadRequestError("One or more groups or members are invalid.");
