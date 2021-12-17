using Bit.Core.Models.Table;

namespace Bit.Admin.Models
{
    public class TaxRatesModel : PagedModel<TaxRate>
    {
        public string Message { get; set; }
    }
}
