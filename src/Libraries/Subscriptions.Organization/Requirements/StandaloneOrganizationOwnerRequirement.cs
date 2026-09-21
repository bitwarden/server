using Microsoft.AspNetCore.Authorization;

namespace Bit.Subscriptions.Organization.Requirements;

/// <summary>
/// Authorizes only an owner of a standalone organization.
/// Satisfied by <see cref="StandaloneOrganizationOwnerRequirementHandler"/>.
/// </summary>
public class StandaloneOrganizationOwnerRequirement : IAuthorizationRequirement;
