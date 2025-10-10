using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Microsoft.Azure.NotificationHubs.Messaging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

/// <summary>
/// Handles creating or updating organization policies with validation and side effect execution.
/// </summary>
/// <remarks>
/// Workflow:
/// 1. Validates organization can use policies
/// 2. Validates required and dependent policies
/// 3. Runs policy-specific validation (<see cref="IPolicyValidationEvent"/>)
/// 4. Executes pre-save logic (<see cref="IOnPolicyPreUpdateEvent"/>)
/// 5. Saves the policy
/// 6. Logs the event
/// 7. Executes post-save logic (<see cref="IOnPolicyPostUpdateEvent"/>)
/// </remarks>
public interface IVNextSavePolicyCommand
{
    /// <summary>
    /// Performs the necessary validations, saves the policy and any side effects
    /// </summary>
    /// <param name="policyRequest">Policy data, acting user, and metadata.</param>
    /// <returns>The saved policy with updated revision and applied changes.</returns>
    /// <exception cref="BadRequestException">
    /// Thrown if:
    /// - The organization can’t use policies
    /// - Dependent policies are missing or block changes
    /// - Custom validation fails
    /// </exception>
    Task<Policy> SaveAsync(SavePolicyModel policyRequest);
}
