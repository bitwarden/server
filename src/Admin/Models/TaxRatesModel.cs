using Bit.Core.Entities;

namespace Bit.Admin.Models;

public class TaxRatesModel : PagedModel<TaxRate>
{
    public string Message { get; set; }
}
