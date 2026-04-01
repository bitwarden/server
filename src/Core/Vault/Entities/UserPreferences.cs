#nullable enable

using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Vault.Entities;

public class UserPreferences : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Data { get; set; } = null!;
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    /// <summary>
    /// Creates a new user preferences instance with a generated id.
    /// </summary>
    /// <param name="userId">The id of the user</param>
    /// <param name="data">The encrypted preferences data</param>
    /// <returns>A new user preferences instance</returns>
    public static UserPreferences Create(Guid userId, string data)
    {
        var preferences = new UserPreferences
        {
            UserId = userId,
            Data = data,
        };
        preferences.SetNewId();
        return preferences;
    }

    /// <summary>
    /// Updates the preferences data and bumps the revision date.
    /// </summary>
    /// <param name="data">The encrypted preferences data</param>
    public void Update(string data)
    {
        Data = data;
        RevisionDate = DateTime.UtcNow;
    }
}
