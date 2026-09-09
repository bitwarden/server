namespace Bit.Seeder.Models;

/// <summary>
/// Internal result produced by the recipe pipeline, carrying entity IDs and counts before being mapped to a public result type.
/// </summary>
/// <remarks>
/// <see cref="GatewayCustomerId"/> and <see cref="GatewaySubscriptionId"/> default to null because they
/// are populated by a post-commit step, after the rest of the result has been snapshotted. See
/// <c>RecipeExecutor.ExecuteAsync</c>.
/// </remarks>
internal record PipelineExecutionResult(
    Guid? OrganizationId,
    Guid? UserId,
    string? OwnerEmail,
    string? UserApiKey,
    string? OrganizationApiKey,
    string? Password,
    bool Premium,
    int UsersCount,
    int GroupsCount,
    int CollectionsCount,
    int CiphersCount,
    int FoldersCount,
    string? SsoIdentifier,
    string? GatewayCustomerId = null,
    string? GatewaySubscriptionId = null);
