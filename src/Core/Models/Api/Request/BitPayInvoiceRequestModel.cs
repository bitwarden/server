using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class BitPayInvoiceRequestModel : IValidatableObject
    {
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public bool Credit { get; set; }
        [Required]
        public decimal? Amount { get; set; }
        public string ReturnUrl { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }

        public NBitpayClient.Invoice ToBitpayClientInvoice(GlobalSettings globalSettings)
        {
            var inv = new NBitpayClient.Invoice
            {
                Price = Amount.Value,
                Currency = "USD",
                RedirectURL = ReturnUrl,
                BuyerEmail = Email,
                Buyer = new NBitpayClient.Buyer
                {
                    email = Email,
                    Name = Name
                },
                NotificationURL = globalSettings.BitPay.NotificationUrl,
                FullNotifications = true,
                ExtendedNotifications = true
            };

            var posData = string.Empty;
            if(UserId.HasValue)
            {
                posData = "userId:" + UserId.Value;
            }
            else if(OrganizationId.HasValue)
            {
                posData = "organizationId:" + OrganizationId.Value;
            }

            if(Credit)
            {
                posData += ",accountCredit:1";
                inv.ItemDesc = "Bitwarden Account Credit";
            }
            else
            {
                inv.ItemDesc = "Bitwarden";
            }

            inv.PosData = posData;
            return inv;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(!UserId.HasValue && !OrganizationId.HasValue)
            {
                yield return new ValidationResult("User or Ooganization is required.");
            }
        }
    }
}
