using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

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
        public string BillingEmail { get; set; }
        public PlanType PlanType { get; set; }
        [Required]
        public string Key { get; set; }
        public string CardToken { get; set; }
        [Range(0, double.MaxValue)]
        public short AdditionalUsers { get; set; }
        public bool Monthly { get; set; }

        public virtual OrganizationSignup ToOrganizationSignup(User user)
        {
            return new OrganizationSignup
            {
                Owner = user,
                OwnerKey = Key,
                Name = Name,
                Plan = PlanType,
                PaymentToken = CardToken,
                AdditionalUsers = AdditionalUsers,
                BillingEmail = BillingEmail,
                BusinessName = BusinessName,
                Monthly = Monthly
            };
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(PlanType != PlanType.Free && string.IsNullOrWhiteSpace(CardToken))
            {
                yield return new ValidationResult("Card required.", new string[] { nameof(CardToken) });
            }
        }
    }
}
