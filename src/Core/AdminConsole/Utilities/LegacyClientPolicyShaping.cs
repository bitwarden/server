using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Utilities;

/// <summary>
/// Hides policy <c>Data</c> shapes from clients that cannot parse them (PM-35389).
/// </summary>
/// <remarks>
/// <para>
/// Clients older than <see cref="Constants.PM35389RestrictedItemTypesDataMinimumVersion"/> cast the
/// <see cref="PolicyType.RestrictedItemTypesPolicy"/> data straight to a list of cipher types and throw on the
/// <c>{ "restrictedTypes": [...] }</c> object. For them the data is replaced with <c>null</c>, which they already read
/// as "Card restricted". The projection never mutates the stored entity and does not depend on a feature flag, so
/// rolling a flag back never re-exposes the object to an old client.
/// </para>
/// <para>
/// Apply this to every <b>member-facing</b> endpoint that serializes policies into client state. Admin endpoints
/// (<c>PolicyStatusResponseModel</c>, <c>PUT /policies/{type}</c>) must keep returning the real data. A new
/// member-facing endpoint that returns policies needs to be considered for this shim.
/// </para>
/// </remarks>
public static class LegacyClientPolicyShaping
{
    private static readonly Version _minimumVersion = new(Constants.PM35389RestrictedItemTypesDataMinimumVersion);

    /// <summary>
    /// Whether the calling client can safely receive the object-shaped data of the restricted item types policy.
    /// </summary>
    /// <remarks>
    /// Mobile clients ignore the data (they only read the policy's presence), so they are never shaped. Every other
    /// client is shaped unless it reports a version at or above the minimum. Fails safe: an absent or unrecognized
    /// device type, or a missing version, is treated as a client that cannot parse the shape. The version header is
    /// a compatibility signal, not a security boundary.
    /// </remarks>
    public static bool CanParseRestrictedItemTypesData(DeviceType? deviceType, Version? clientVersion) =>
        DeviceTypes.ToClientType(deviceType) == ClientType.Mobile
        || (clientVersion is not null && clientVersion >= _minimumVersion);

    /// <summary>
    /// Returns the policies as the calling client should see them. Policies that need no change are returned as the
    /// same instances; changed ones are copies, so stored entities are never modified.
    /// </summary>
    public static IEnumerable<Policy> ShapeForClient(
        IEnumerable<Policy> policies, DeviceType? deviceType, Version? clientVersion) =>
        CanParseRestrictedItemTypesData(deviceType, clientVersion)
            ? policies
            : policies.Select(WithoutRestrictedItemTypesData);

    /// <summary>
    /// Returns the policy as it should appear in an organization-wide broadcast, where the recipients' versions are
    /// unknown. Clients that predate the sync-on-push behavior apply the payload directly, so the object-shaped data
    /// is withheld; current clients ignore the payload and run a full sync, which is shaped per client.
    /// </summary>
    public static Policy ShapeForBroadcast(Policy policy) => WithoutRestrictedItemTypesData(policy);

    private static Policy WithoutRestrictedItemTypesData(Policy policy) =>
        policy is { Type: PolicyType.RestrictedItemTypesPolicy, Data: not null }
            ? new Policy
            {
                Id = policy.Id,
                OrganizationId = policy.OrganizationId,
                Type = policy.Type,
                Enabled = policy.Enabled,
                Data = null,
                CreationDate = policy.CreationDate,
                RevisionDate = policy.RevisionDate,
            }
            : policy;
}
