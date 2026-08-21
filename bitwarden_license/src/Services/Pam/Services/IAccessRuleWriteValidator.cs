using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

public interface IAccessRuleWriteValidator
{
    /// <summary>
    /// Validates a rule that is about to be persisted — its own fields, its conditions document, its name's
    /// uniqueness within the organization, and the collections it is to govern — and returns the deduplicated
    /// collection ids to associate with it.
    /// </summary>
    /// <param name="organizationId">The organization the rule belongs to, and the only one its collections may
    /// belong to.</param>
    /// <param name="rule">The rule as it will be persisted.</param>
    /// <param name="collectionIds">The complete set of collections the rule should govern.</param>
    /// <param name="existingRuleId">The id of the rule being updated, or null when creating. An update is excluded
    /// from its own name-uniqueness check and may keep the collections it already governs, whereas a create
    /// conflicts with any already-governed collection.</param>
    /// <returns>
    /// The deduplicated collection ids, or the <em>first</em> failure found: one of
    /// <see cref="Errors.AccessRuleNameRequired"/>, <see cref="Errors.AccessRuleExtensionLengthRequired"/>,
    /// <see cref="Errors.AccessRuleDefaultDurationMustBePositive"/>,
    /// <see cref="Errors.AccessRuleMaxDurationMustBePositive"/>,
    /// <see cref="Errors.AccessRuleDefaultDurationExceedsMax"/>, <see cref="Errors.AccessRuleInvalidConditions"/>,
    /// <see cref="Errors.AccessRuleNameTaken"/>, <see cref="Errors.AccessRuleCollectionsMissing"/>,
    /// <see cref="Errors.AccessRuleCollectionsForeign"/> or
    /// <see cref="Errors.AccessRuleCollectionsAlreadyGoverned"/>. Only the first is reported, so a form correcting
    /// one failure may surface the next.
    /// </returns>
    Task<CommandResult<List<Guid>>> ValidateAsync(Guid organizationId, AccessRule rule, IEnumerable<Guid> collectionIds,
        Guid? existingRuleId = null);
}
