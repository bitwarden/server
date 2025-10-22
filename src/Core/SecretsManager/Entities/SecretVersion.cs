#nullable enable
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.SecretsManager.Entities;

public class SecretVersion : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid SecretId { get; set; }

    public string Value { get; set; } = string.Empty;

    public DateTime VersionDate { get; set; }

    public Guid? EditorServiceAccountId { get; set; }

    public Guid? EditorOrganizationUserId { get; set; }

    public void SetNewId()
    {
        if (Id == default(Guid))
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
