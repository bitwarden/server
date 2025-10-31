// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Entities;

public class CipherHistory : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid CipherId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public CipherType Type { get; set; }
    public string Data { get; set; }
    public string Favorites { get; set; }
    public string Folders { get; set; }
    public string Attachments { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime? DeletedDate { get; set; }
    public CipherRepromptType? Reprompt { get; set; }
    public string Key { get; set; }
    public DateTime? ArchivedDate { get; set; }
    public DateTime HistoryDate { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
