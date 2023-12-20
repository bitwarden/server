using System.Net;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities.Provider;

public class Provider : ITableObject<Guid>
{
    public Guid Id { get; set; }
    /// <summary>
    /// For display purposes use the method ProviderName() instead.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// For display purposes use the method ProviderBusinessName() instead.
    /// </summary>
    public string BusinessName { get; set; }
    public string BusinessAddress1 { get; set; }
    public string BusinessAddress2 { get; set; }
    public string BusinessAddress3 { get; set; }
    public string BusinessCountry { get; set; }
    public string BusinessTaxNumber { get; set; }
    public string BillingEmail { get; set; }
    public string BillingPhone { get; set; }
    public ProviderStatusType Status { get; set; }
    public bool UseEvents { get; set; }
    public ProviderType Type { get; set; }
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

    /// <summary>
    /// Returns the name of the provider, decoded from HTML ready for display
    /// </summary>
    public string ProviderName()
    {
        return WebUtility.HtmlDecode(Name);
    }

    /// <summary>
    /// Returns the business name of the provider, decoded from HTML ready for display
    /// </summary>
    public string ProviderBusinessName()
    {
        return WebUtility.HtmlDecode(BusinessName);
    }
}
