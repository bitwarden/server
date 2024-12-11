using System.ComponentModel.DataAnnotations;

#nullable enable

namespace Bit.Core.Entities;

public class TaxRate : ITableObject<string>
{
    [MaxLength(40)]
    public string Id { get; set; } = null!;

    [MaxLength(50)]
    public string Country { get; set; } = null!;

    [MaxLength(2)]
    public string? State { get; set; }

    [MaxLength(10)]
    public string PostalCode { get; set; } = null!;
    public decimal Rate { get; set; }
    public bool Active { get; set; }

    public void SetNewId()
    {
        // Id is created by Stripe, should exist before this gets called
        return;
    }
}
