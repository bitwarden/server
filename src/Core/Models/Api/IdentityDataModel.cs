using System;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class IdentityDataModel : CipherDataModel
    {
        public IdentityDataModel() { }

        public IdentityDataModel(CipherRequestModel cipher)
        {
            Name = cipher.Name;
            Notes = cipher.Notes;
            Fields = cipher.Fields;

            Title = cipher.Identity.Title;
            FirstName = cipher.Identity.FirstName;
            MiddleName = cipher.Identity.MiddleName;
            LastName = cipher.Identity.LastName;
            Address1 = cipher.Identity.Address1;
            Address2 = cipher.Identity.Address2;
            Address3 = cipher.Identity.Address3;
            City = cipher.Identity.City;
            State = cipher.Identity.State;
            PostalCode = cipher.Identity.PostalCode;
            Country = cipher.Identity.Country;
            Company = cipher.Identity.Company;
            Email = cipher.Identity.Email;
            Phone = cipher.Identity.Phone;
            SSN = cipher.Identity.SSN;
            Username = cipher.Identity.Username;
            PassportNumber = cipher.Identity.PassportNumber;
            LicenseNumber = cipher.Identity.LicenseNumber;
        }

        public IdentityDataModel(Cipher cipher)
        {
            if(cipher.Type != Enums.CipherType.Identity)
            {
                throw new ArgumentException("Cipher is not correct type.");
            }

            var data = JsonConvert.DeserializeObject<IdentityDataModel>(cipher.Data);

            Name = data.Name;
            Notes = data.Notes;
            Fields = data.Fields;

            Title = data.Title;
            FirstName = data.FirstName;
            MiddleName = data.MiddleName;
            LastName = data.LastName;
            Address1 = data.Address1;
            Address2 = data.Address2;
            Address3 = data.Address3;
            City = data.City;
            State = data.State;
            PostalCode = data.PostalCode;
            Country = data.Country;
            Company = data.Company;
            Email = data.Email;
            Phone = data.Phone;
            SSN = data.SSN;
            Username = data.Username;
            PassportNumber = data.PassportNumber;
            LicenseNumber = data.LicenseNumber;
        }

        public string Title { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string Address3 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public string Company { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string SSN { get; set; }
        public string Username { get; set; }
        public string PassportNumber { get; set; }
        public string LicenseNumber { get; set; }
    }
}
