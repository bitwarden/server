using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public class CipherCardData : CipherData
    {
        public CipherCardData() { }

        public CipherCardData(CipherRequestModel cipher)
            : base(cipher)
        {
            CardholderName = cipher.Card.CardholderName;
            Brand = cipher.Card.Brand;
            Number = cipher.Card.Number;
            ExpMonth = cipher.Card.ExpMonth;
            ExpYear = cipher.Card.ExpYear;
            Code = cipher.Card.Code;
        }

        public string CardholderName { get; set; }
        public string Brand { get; set; }
        public string Number { get; set; }
        public string ExpMonth { get; set; }
        public string ExpYear { get; set; }
        public string Code { get; set; }
    }
}
