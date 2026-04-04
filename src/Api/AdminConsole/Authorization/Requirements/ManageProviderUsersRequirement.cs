namespace Bit.Api.AdminConsole.Authorization.Requirements;

/// <summary>
/// Authorizes users who can manage provider users (ProviderAdmin only).
/// </summary>
public class ManageProviderUsersRequirement : ProviderAdminRequirement;
