﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationCreateRequestModel : IValidatableObject
{
    [Required]
    [StringLength(50, ErrorMessage = "The field Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string Name { get; set; }

    [StringLength(50, ErrorMessage = "The field Business Name exceeds the maximum length.")]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string BusinessName { get; set; }

    [Required]
    [StringLength(256)]
    [EmailAddress]
    public string BillingEmail { get; set; }

    public PlanType PlanType { get; set; }

    [Required]
    public string Key { get; set; }

    public OrganizationKeysRequestModel Keys { get; set; }
    public PaymentMethodType? PaymentMethodType { get; set; }
    public string PaymentToken { get; set; }

    [Range(0, int.MaxValue)]
    public int AdditionalSeats { get; set; }

    [Range(0, 99)]
    public short? AdditionalStorageGb { get; set; }

    public bool PremiumAccessAddon { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string CollectionName { get; set; }

    public string TaxIdNumber { get; set; }

    public string BillingAddressLine1 { get; set; }

    public string BillingAddressLine2 { get; set; }

    public string BillingAddressCity { get; set; }

    public string BillingAddressState { get; set; }

    public string BillingAddressPostalCode { get; set; }

    [StringLength(2)]
    public string BillingAddressCountry { get; set; }

    public int? MaxAutoscaleSeats { get; set; }

    [Range(0, int.MaxValue)]
    public int? AdditionalSmSeats { get; set; }

    [Range(0, int.MaxValue)]
    public int? AdditionalServiceAccounts { get; set; }

    [Required]
    public bool UseSecretsManager { get; set; }

    public bool IsFromSecretsManagerTrial { get; set; }

    public string InitiationPath { get; set; }

    public virtual OrganizationSignup ToOrganizationSignup(User user)
    {
        var orgSignup = new OrganizationSignup
        {
            Owner = user,
            OwnerKey = Key,
            Name = Name,
            Plan = PlanType,
            PaymentMethodType = PaymentMethodType,
            PaymentToken = PaymentToken,
            AdditionalSeats = AdditionalSeats,
            MaxAutoscaleSeats = MaxAutoscaleSeats,
            AdditionalStorageGb = AdditionalStorageGb.GetValueOrDefault(0),
            PremiumAccessAddon = PremiumAccessAddon,
            BillingEmail = BillingEmail,
            BusinessName = BusinessName,
            CollectionName = CollectionName,
            AdditionalSmSeats = AdditionalSmSeats.GetValueOrDefault(),
            AdditionalServiceAccounts = AdditionalServiceAccounts.GetValueOrDefault(),
            UseSecretsManager = UseSecretsManager,
            IsFromSecretsManagerTrial = IsFromSecretsManagerTrial,
            TaxInfo = new TaxInfo
            {
                TaxIdNumber = TaxIdNumber,
                BillingAddressLine1 = BillingAddressLine1,
                BillingAddressLine2 = BillingAddressLine2,
                BillingAddressCity = BillingAddressCity,
                BillingAddressState = BillingAddressState,
                BillingAddressPostalCode = BillingAddressPostalCode,
                BillingAddressCountry = BillingAddressCountry,
            },
            InitiationPath = InitiationPath,
        };

        Keys?.ToOrganizationSignup(orgSignup);

        return orgSignup;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PlanType != PlanType.Free && string.IsNullOrWhiteSpace(PaymentToken))
        {
            yield return new ValidationResult("Payment required.", new string[] { nameof(PaymentToken) });
        }

        if (PlanType != PlanType.Free && !PaymentMethodType.HasValue)
        {
            yield return new ValidationResult("Payment method type required.",
                new string[] { nameof(PaymentMethodType) });
        }

        if (PlanType != PlanType.Free && string.IsNullOrWhiteSpace(BillingAddressCountry))
        {
            yield return new ValidationResult("Country required.",
                new string[] { nameof(BillingAddressCountry) });
        }

        if (PlanType != PlanType.Free && BillingAddressCountry == "US" &&
            string.IsNullOrWhiteSpace(BillingAddressPostalCode))
        {
            yield return new ValidationResult("Zip / postal code is required.",
                new string[] { nameof(BillingAddressPostalCode) });
        }
    }
}

