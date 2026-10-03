using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Utilities;
using Bit.Invoicing.InvoicePreviews.Models;

namespace Bit.Subscriptions.Organization.Models.Requests;

internal record PreviewOrganizationPlanChangeRequest
{
    [JsonConverter(typeof(EnumMemberJsonConverter<PlanTierType>))]
    public required PlanTierType Tier { get; init; }

    [JsonConverter(typeof(EnumMemberJsonConverter<PlanCadenceType>))]
    public required PlanCadenceType Cadence { get; init; }

    public BillingAddress? BillingAddress { get; init; }
}
