namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

/// <summary>
/// The position of the last entitlement read in <c>(RevisionDate, Id)</c> order. The next page starts after it.
/// </summary>
/// <param name="RevisionDate">The RevisionDate exactly as read from the database.</param>
public readonly record struct PartnershipEntitlementKeyset(DateTime RevisionDate, Guid Id);
