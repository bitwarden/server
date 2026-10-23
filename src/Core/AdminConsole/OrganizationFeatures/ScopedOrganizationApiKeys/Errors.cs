using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

public record ScopedApiKeyOrganizationNotFound()
    : NotFoundError("Organization not found.");

public record ScopedApiKeyNotFound()
    : NotFoundError("Scoped API key not found.");

public record ScopedApiKeyApiNotAvailable()
    : BadRequestError("Your organization's plan does not support API access.");

public record ScopedApiKeyNameRequired()
    : BadRequestError("A name is required.");

public record ScopedApiKeyNameTooLong()
    : BadRequestError($"The name must be {CreateScopedOrganizationApiKeyCommand.MaxNameLength} characters or fewer.");

public record ScopedApiKeyScopesRequired()
    : BadRequestError("At least one scope is required.");

public record ScopedApiKeyScopesInvalid()
    : BadRequestError("One or more scopes can't be granted to a scoped API key.");

public record ScopedApiKeyScopesDuplicated()
    : BadRequestError("Scopes can't contain duplicates.");

public record ScopedApiKeyExpirationNotInFuture()
    : BadRequestError("The expiration date must be in the future.");

public record ScopedApiKeyLimitReached()
    : BadRequestError($"An organization can have at most {CreateScopedOrganizationApiKeyCommand.MaxKeysPerOrganization} scoped API keys.");
