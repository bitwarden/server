using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class PremiumRequestModel : PaymentRequestModel
    {
        [Range(0, 99)]
        public short? AdditionalStorageGb { get; set; }
    }
}
