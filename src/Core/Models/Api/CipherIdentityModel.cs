using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class CipherIdentityModel
    {
        public CipherIdentityModel() { }

        public CipherIdentityModel(CipherIdentityData data)
        {
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

        [EncryptedString]
        [StringLength(1000)]
        public string Title { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string FirstName { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string MiddleName { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string LastName { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Address1 { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Address2 { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Address3 { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string City { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string State { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string PostalCode { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Country { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Company { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Email { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Phone { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string SSN { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Username { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string PassportNumber { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string LicenseNumber { get; set; }
    }
}
