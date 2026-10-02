using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;

/// <summary>
/// The decision from evaluating a single policy against a user who is about to be granted access to an organization.
/// See <see cref="IPreAccessPolicyEnforcer"/>.
/// </summary>
public sealed record PreAccessPolicyDecision
{
    private PreAccessPolicyDecision(bool isEnforced, string? data)
    {
        IsEnforced = isEnforced;
        Data = data;
    }

    /// <summary>
    /// True if the policy must be enforced against the user, false otherwise.
    /// </summary>
    public bool IsEnforced { get; }

    /// <summary>
    /// The policy's data (JSON), if the policy is enforced.
    /// </summary>
    public string? Data { get; }

    /// <summary>
    /// The policy does not apply to the user.
    /// </summary>
    public static PreAccessPolicyDecision NotEnforced { get; } = new(false, null);

    /// <summary>
    /// The policy must be enforced against the user.
    /// </summary>
    /// <param name="data">The policy's data (JSON).</param>
    public static PreAccessPolicyDecision Enforced(string? data) => new(true, data);

    public T GetDataModel<T>() where T : IPolicyDataModel, new()
        => CoreHelpers.LoadClassFromJsonData<T>(Data);
}
