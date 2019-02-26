using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class OrganizationCreateRequestModel : IValidatableObject
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        [StringLength(50)]
        public string BusinessName { get; set; }
        [Required]
        [StringLength(50)]
        [EmailAddress]
        public string BillingEmail { get; set; }
        public PlanType PlanType { get; set; }
        [Required]
        public string Key { get; set; }
        public PaymentMethodType? PaymentMethodType { get; set; }
        public string PaymentToken { get; set; }
        [Range(0, double.MaxValue)]
        public short AdditionalSeats { get; set; }
        [Range(0, 99)]
        public short? AdditionalStorageGb { get; set; }
        public bool PremiumAccessAddon { get; set; }
        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string CollectionName { get; set; }

        public virtual OrganizationSignup ToOrganizationSignup(User user)
        {
            return new OrganizationSignup
            {
                Owner = user,
                OwnerKey = Key,
                Name = Name,
                Plan = PlanType,
                PaymentMethodType = PaymentMethodType,
                PaymentToken = PaymentToken,
                AdditionalSeats = AdditionalSeats,
                AdditionalStorageGb = AdditionalStorageGb.GetValueOrDefault(0),
                PremiumAccessAddon = PremiumAccessAddon,
                BillingEmail = BillingEmail,
                BusinessName = BusinessName,
                CollectionName = CollectionName
            };
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(PlanType != PlanType.Free && string.IsNullOrWhiteSpace(PaymentToken))
            {
                yield return new ValidationResult("Payment required.", new string[] { nameof(PaymentToken) });
            }
            if(PlanType != PlanType.Free && !PaymentMethodType.HasValue)
            {
                yield return new ValidationResult("Payment method type required.",
                    new string[] { nameof(PaymentMethodType) });
            }
        }
    }
}
