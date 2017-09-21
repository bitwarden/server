using System;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class CardDataModel : CipherDataModel
    {
        public CardDataModel() { }

        public CardDataModel(CipherRequestModel cipher)
        {
            Name = cipher.Name;
            Notes = cipher.Notes;
            Fields = cipher.Fields;

            CardholderName = cipher.Card.CardholderName;
            Brand = cipher.Card.Brand;
            Number = cipher.Card.Number;
            ExpMonth = cipher.Card.ExpMonth;
            ExpYear = cipher.Card.ExpYear;
            Code = cipher.Card.Code;
        }

        public CardDataModel(Cipher cipher)
        {
            if(cipher.Type != Enums.CipherType.Card)
            {
                throw new ArgumentException("Cipher is not correct type.");
            }

            var data = JsonConvert.DeserializeObject<CardDataModel>(cipher.Data);

            Name = data.Name;
            Notes = data.Notes;
            Fields = data.Fields;

            CardholderName = data.CardholderName;
            Brand = data.Brand;
            Number = data.Number;
            ExpMonth = data.ExpMonth;
            ExpYear = data.ExpYear;
            Code = data.Code;
        }

        public string CardholderName { get; set; }
        public string Brand { get; set; }
        public string Number { get; set; }
        public string ExpMonth { get; set; }
        public string ExpYear { get; set; }
        public string Code { get; set; }
    }
}
