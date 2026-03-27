using Bit.Core.Models.Api;
using Bit.Core.Vault.Entities;

namespace Bit.Api.Vault.Models.Response;

public class UserPreferencesResponseModel : ResponseModel
{
    public UserPreferencesResponseModel()
        : base("userPreferences")
    {
    }

    public UserPreferencesResponseModel(UserPreferences userPreferences)
        : base("userPreferences")
    {
        ArgumentNullException.ThrowIfNull(userPreferences);

        Id = userPreferences.Id;
        Data = userPreferences.Data;
        RevisionDate = userPreferences.RevisionDate;
    }

    public Guid Id { get; set; }
    public string Data { get; set; }
    public DateTime RevisionDate { get; set; }
}
