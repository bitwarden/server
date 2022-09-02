using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Entities;

public class TaxRate : ITableObject<string>
{
    [MaxLength(40)]
    public string Id { get; set; }
    [MaxLength(50)]
    public string Country { get; set; }
    [MaxLength(2)]
    public string State { get; set; }
    [MaxLength(10)]
    public string PostalCode { get; set; }
    public decimal Rate { get; set; }
    public bool Active { get; set; }

    public void SetNewId()
    {
        // Id is created by Stripe, should exist before this gets called
        return;
    }
}
