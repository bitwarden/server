using System.ComponentModel.DataAnnotations;
using Bit.Core.Settings;

namespace Bit.Api.Models.Request;

public class BitPayInvoiceRequestModel : IValidatableObject
{
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? ProviderId { get; set; }
    public bool Credit { get; set; }

    [Required]
    public decimal? Amount { get; set; }
    public string ReturnUrl { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }

    public BitPayLight.Models.Invoice.Invoice ToBitpayInvoice(GlobalSettings globalSettings)
    {
        var inv = new BitPayLight.Models.Invoice.Invoice
        {
            Price = Convert.ToDouble(Amount.Value),
            Currency = "USD",
            RedirectUrl = ReturnUrl,
            Buyer = new BitPayLight.Models.Invoice.Buyer { Email = Email, Name = Name },
            NotificationUrl = globalSettings.BitPay.NotificationUrl,
            FullNotifications = true,
            ExtendedNotifications = true,
        };

        var posData = string.Empty;
        if (UserId.HasValue)
        {
            posData = "userId:" + UserId.Value;
        }
        else if (OrganizationId.HasValue)
        {
            posData = "organizationId:" + OrganizationId.Value;
        }
        else if (ProviderId.HasValue)
        {
            posData = "providerId:" + ProviderId.Value;
        }

        if (Credit)
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
        if (!UserId.HasValue && !OrganizationId.HasValue && !ProviderId.HasValue)
        {
            yield return new ValidationResult("User, Organization or Provider is required.");
        }
    }
}
