using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public class CipherIdentityData : CipherData
    {
        public CipherIdentityData() { }

        public CipherIdentityData(CipherRequestModel cipher)
            : base(cipher)
        {
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
