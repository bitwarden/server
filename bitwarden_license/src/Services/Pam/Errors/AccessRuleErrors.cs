using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Services.Pam.Errors;

// The access-rule write surface's failures. The Type contract is the one described in AccessRequestErrors.cs —
// stable, never localized, never reworded.
//
// Every one of these names a control on the rule edit form, so PropertyName is the request property rather than
// PamErrorProperties.Code: an admin can correct all of them in place.

/// <summary>The rule does not exist, or belongs to another organization than the one on the route.</summary>
public record AccessRuleNotFound() : NotFoundError();

/// <summary>The rule has no name. Rules are picked out by name wherever they are listed, so it is required.</summary>
public record AccessRuleNameRequired()
    : BadRequestError("Name is required."), IValidationError
{
    public string PropertyName => "name";
    public string Type => "rule_name_required";
}

/// <summary>Another rule in the same organization already has this name, compared case-insensitively.</summary>
public record AccessRuleNameTaken()
    : BadRequestError("A rule with that name already exists."), IValidationError
{
    public string PropertyName => "name";
    public string Type => "rule_name_taken";
}

/// <summary>The rule allows extensions without capping them, which would leave every extension unbounded.</summary>
public record AccessRuleExtensionLengthRequired()
    : BadRequestError("A maximum extension length is required when extensions are allowed."), IValidationError
{
    public string PropertyName => "maxExtensionDurationSeconds";
    public string Type => "extension_length_required";
}

/// <summary>The rule's default lease duration is zero or negative.</summary>
public record AccessRuleDefaultDurationMustBePositive()
    : BadRequestError("The default lease duration must be a positive value."), IValidationError
{
    public string PropertyName => "defaultLeaseDurationSeconds";
    public string Type => "rule_default_duration_must_be_positive";
}

/// <summary>The rule's maximum lease duration is zero or negative.</summary>
public record AccessRuleMaxDurationMustBePositive()
    : BadRequestError("The maximum lease duration must be a positive value."), IValidationError
{
    public string PropertyName => "maxLeaseDurationSeconds";
    public string Type => "rule_max_duration_must_be_positive";
}

/// <summary>
/// The rule pre-fills requests with a duration its own cap would then refuse, so every request against it would
/// fail at submit.
/// </summary>
public record AccessRuleDefaultDurationExceedsMax()
    : BadRequestError("The default lease duration cannot exceed the maximum lease duration."), IValidationError
{
    public string PropertyName => "defaultLeaseDurationSeconds";
    public string Type => "rule_default_duration_exceeds_max";
}

/// <summary>
/// The rule's conditions document did not validate. The detail is the validator's own sentence, which names the
/// offending condition — a client that builds the document itself should treat this as its own bug.
/// </summary>
public record AccessRuleInvalidConditions(string Detail)
    : BadRequestError(Detail), IValidationError
{
    public string PropertyName => "conditions";
    public string Type => "rule_invalid_conditions";
}

/// <summary>One or more of the collections the rule would govern does not exist.</summary>
public record AccessRuleCollectionsMissing()
    : BadRequestError("One or more collections could not be found."), IValidationError
{
    public string PropertyName => "collections";
    public string Type => "collections_missing";
}

/// <summary>One or more of the collections the rule would govern belongs to another organization.</summary>
public record AccessRuleCollectionsForeign()
    : BadRequestError("One or more collections do not belong to this organization."), IValidationError
{
    public string PropertyName => "collections";
    public string Type => "collections_foreign";
}

/// <summary>
/// One or more of the collections the rule would govern is already governed by a different rule. A collection has
/// at most one access rule.
/// </summary>
public record AccessRuleCollectionsAlreadyGoverned()
    : BadRequestError("One or more collections are already governed by another access rule."), IValidationError
{
    public string PropertyName => "collections";
    public string Type => "collections_already_governed";
}
