using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections;

public record CollectionAccessInvalidError() : BadRequestError("One or more groups or members are invalid.");

/// <summary>
/// Returned when a collection access selection violates
/// <see cref="Bit.Core.Models.Data.CollectionAccessSelection.Valid"/>. Shared by every feature that
/// assigns collection access, so the invariant reports identically wherever it is enforced.
/// </summary>
public record ManageMutuallyExclusive() : BadRequestError(
    "The Manage property is mutually exclusive and cannot be true while the ReadOnly or HidePasswords properties are also true.");
