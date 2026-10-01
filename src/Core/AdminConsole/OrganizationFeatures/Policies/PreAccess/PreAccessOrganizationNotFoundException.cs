using Bit.Core.Exceptions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;

/// <summary>
/// Thrown by <see cref="IPreAccessEnforcerQuery"/> when the target organization does not exist.
/// </summary>
public class PreAccessOrganizationNotFoundException()
    : NotFoundException("Organization not found. Unable to evaluate its policies.");
