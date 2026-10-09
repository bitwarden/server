using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

/// <summary>
/// A partnership error with a stable, machine-readable code for the API layer to map.
/// </summary>
public interface IPartnershipError
{
    string Code { get; }
}

/// <summary>
/// A rejected activation, reported as a binding failure with this reason.
/// </summary>
public interface IPartnershipBindingFailure
{
    PartnershipEntitlementReason Reason { get; }
}

public abstract record PartnershipValidationError(string Message, string PropertyName, string Type)
    : BadRequestError(Message), IValidationError, IPartnershipError
{
    public string Code => Type;
}

public record InvalidExternalId()
    : PartnershipValidationError(
        $"External ID is required, must be at most {OrganizationPartnershipEntitlement.ExternalIdMaxLength} characters, and must not start with \"{Constants.DatabaseFieldProtectedPrefix}\".",
        "externalId", "invalid_external_id");

public record TooManyMetadataKeys()
    : PartnershipValidationError(
        $"Metadata may have at most {ProvisionPartnershipEntitlementRequest.MaxMetadataKeys} keys.",
        "metadata", "too_many_metadata_keys");

public record InvalidEntitlementReason()
    : PartnershipValidationError("The reason is not valid for this action.", "reason", "invalid_reason");

public record UserIdRequired()
    : PartnershipValidationError("A user is required for this action.", "userId", "user_id_required");

public record InvalidPartnershipName()
    : PartnershipValidationError(
        $"Name is required and must be at most {CreateOrganizationPartnershipRequest.NameMaxLength} characters.",
        "name", "invalid_name");

public record UnsupportedBindingMode()
    : PartnershipValidationError("Only token binding is supported.", "bindingMode", "unsupported_binding_mode");

public record UnsupportedSponsoredPlanType()
    : PartnershipValidationError(
        "Sponsored plan type is not supported.", "sponsoredPlanType", "unsupported_sponsored_plan_type");

public record InvalidReturnOrigin()
    : PartnershipValidationError(
        "Each return origin must be an absolute https origin with no path, query, or fragment.",
        "registeredReturnOrigins", "invalid_return_origin");

public record EffectiveAtInFuture()
    : BadRequestError("effectiveAt cannot be in the future."), IPartnershipError
{
    public string Code => "effective_at_in_future";
}

public record OrganizationNotFound() : NotFoundError("Organization not found."), IPartnershipError
{
    public string Code => "not_found";
}

public record PartnershipNotFound() : NotFoundError("Partnership not found."), IPartnershipError
{
    public string Code => "not_found";
}

public record EntitlementNotFound() : NotFoundError("Entitlement not found."), IPartnershipError
{
    public string Code => "not_found";
}

public record PartnershipAlreadyExists()
    : ConflictError("This organization already has a partnership."), IPartnershipError
{
    public string Code => "partnership_already_exists";
}

public record PartnershipNotActive() : ConflictError("The partnership is not active."), IPartnershipError
{
    public string Code => "partnership_not_active";
}

public record IllegalEntitlementTransition()
    : ConflictError("The entitlement cannot make this transition from its current state."), IPartnershipError
{
    public string Code => "illegal_transition";
}

public record EntitlementNotResumable()
    : ConflictError("The entitlement's resume window has expired."), IPartnershipError, IPartnershipBindingFailure
{
    public string Code => "entitlement_not_resumable";
    public PartnershipEntitlementReason Reason => PartnershipEntitlementReason.ResumeWindowExpired;
}

public record EntitlementAlreadyBound()
    : ConflictError("The entitlement is already bound to an account."), IPartnershipError, IPartnershipBindingFailure
{
    public string Code => "already_bound";
    public PartnershipEntitlementReason Reason => PartnershipEntitlementReason.AlreadyBound;
}

public record EntitlementNotProvisioned()
    : NotFoundError("The entitlement has not been provisioned."), IPartnershipError, IPartnershipBindingFailure
{
    public string Code => "not_provisioned";
    public PartnershipEntitlementReason Reason => PartnershipEntitlementReason.NotProvisioned;
}
