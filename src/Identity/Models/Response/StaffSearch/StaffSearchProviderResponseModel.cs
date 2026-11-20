using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.HttpExtensions;

namespace Bit.Identity.Models.Response.StaffSearch;

/// <summary>
/// A provider the internal staff search found: what the Admin Portal's providers index shows of it,
/// and nothing else.
/// </summary>
public class StaffSearchProviderResponseModel : ResponseModel
{
    public StaffSearchProviderResponseModel(Provider provider)
        : base("staffSearchProvider")
    {
        ArgumentNullException.ThrowIfNull(provider);

        Id = provider.Id;
        Name = provider.Name;
        Type = provider.Type;
        Status = provider.Status;
        CreationDate = provider.CreationDate;
    }

    public Guid Id { get; set; }

    /// <summary>
    /// The name as this server stores it (HTML-encoded), as its other APIs answer it; <see cref="Provider.DisplayName"/> decodes it. Null for a provider
    /// not yet set up, which the index shows as "Pending".
    /// </summary>
    public string? Name { get; set; }

    public ProviderType Type { get; set; }
    public ProviderStatusType Status { get; set; }
    public DateTime CreationDate { get; set; }
}
