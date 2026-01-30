using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Microsoft.Data.SqlClient;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IInitPendingOrganizationCommand
{
    /// <summary>
    /// Update an Organization entry by setting the public/private keys, set it as 'Enabled' and move the Status from 'Pending' to 'Created'.
    /// </summary>
    /// <remarks>
    /// This method must target a disabled Organization that has null keys and status as 'Pending'.
    /// </remarks>
    [Obsolete("Use InitPendingOrganizationVNextAsync for consolidated flow with upfront validation. This method will be removed.")]
    Task InitPendingOrganizationAsync(User user, Guid organizationId, Guid organizationUserId, string publicKey, string privateKey, string collectionName, string emailToken);

    /// <summary>
    /// Initializes a pending organization and confirms the first owner with upfront validation.
    /// </summary>
    /// <remarks>
    /// The user initializing the organization is the first user to access it - there is no existing 
    /// owner or provider who can change its settings. Therefore, validation in this command assumes 
    /// a default state. For example, it does not enforce policies for this organization because none 
    /// will be enabled yet.
    /// </remarks>
    /// <returns>A CommandResult indicating success or specific validation errors.</returns>
    Task<CommandResult> InitPendingOrganizationVNextAsync(InitPendingOrganizationRequest request);
}

/// <summary>
/// Represents a database update action to be executed during organization initialization.
/// </summary>
public delegate Task OrganizationInitializationUpdateAction(SqlConnection? connection = null,
    SqlTransaction? transaction = null,
    object? context = null);
