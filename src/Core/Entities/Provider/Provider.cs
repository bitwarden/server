using Bit.Core.Enums.Provider;
using Bit.Core.Utilities;

namespace Bit.Core.Entities.Provider;

public class Provider : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string BusinessName { get; set; }
    public string BusinessAddress1 { get; set; }
    public string BusinessAddress2 { get; set; }
    public string BusinessAddress3 { get; set; }
    public string BusinessCountry { get; set; }
    public string BusinessTaxNumber { get; set; }
    public string BillingEmail { get; set; }
    public ProviderStatusType Status { get; set; }
    public bool UseEvents { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
