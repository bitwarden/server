using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyStrategy
{
    /// <summary>
    /// The PolicyType that the strategy is responsible for handling.
    /// </summary>
    public PolicyType Type { get; }

    /// <summary>
    /// A method that is called when the policy state changes from disabled to enabled, before
    /// it is saved to the database.
    /// For example, this can be used for validation before saving or for side effects.
    /// </summary>
    /// <param name="policy">The updated policy object.</param>
    /// <param name="savingUserId">The current user who is updating the policy.</param>
    public Task HandleEnable(Policy policy, Guid? savingUserId);

    /// <summary>
    /// A method that is called when the policy state changes from enabled to disabled, before
    /// it is saved to the database.
    /// For example, this can be used for validation before saving or for side effects.
    /// </summary>
    /// <param name="policy">The updated policy object.</param>
    /// <param name="savingUserId">The current user who is updating the policy.</param>
    public Task HandleDisable(Policy policy, Guid? savingUserId);
}
