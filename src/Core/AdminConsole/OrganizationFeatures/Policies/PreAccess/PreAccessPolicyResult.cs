using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;

/// <summary>
/// The result of evaluating a single policy against a user who is about to be granted access to an organization.
/// See <see cref="IPreAccessPolicyEnforcer"/>.
/// </summary>
/// <param name="Enforced">True if the policy must be enforced against the user, false otherwise.</param>
/// <param name="Data">The policy's data (JSON), if the policy is enforced.</param>
public record PreAccessPolicyResult(bool Enforced, string? Data = null)
{
    public static PreAccessPolicyResult NotEnforced { get; } = new(false);

    public T GetDataModel<T>() where T : IPolicyDataModel, new()
        => CoreHelpers.LoadClassFromJsonData<T>(Data);
}
