using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Entities;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.AdminConsole.Entities.Provider;

public class ProviderUser : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Key { get; set; }
    public ProviderUserStatusType Status { get; set; }
    public ProviderUserType Type { get; set; }
    public string? Permissions { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
