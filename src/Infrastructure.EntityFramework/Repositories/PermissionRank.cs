namespace Bit.Infrastructure.EntityFramework.Repositories;

/// <summary>
/// Weights that order a grant by Manage, then Edit, then ViewPassword, so the highest permission level wins when grants are combined.
/// </summary>
internal static class PermissionRank
{
    public const int Manage = 4;
    public const int Edit = 2;
    public const int ViewPassword = 1;

    /// <summary>
    /// The rank of a collection the user has no grant on: not read-only, passwords visible.
    /// </summary>
    public const int Unassigned = Edit | ViewPassword;

    public static int Of(bool manage, bool readOnly, bool hidePasswords) =>
        (manage ? Manage : 0) + (readOnly ? 0 : Edit) + (hidePasswords ? 0 : ViewPassword);
}
