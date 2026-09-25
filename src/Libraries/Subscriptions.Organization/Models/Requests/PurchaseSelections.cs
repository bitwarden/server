using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;

namespace Bit.Subscriptions.Organization.Models.Requests;

internal record PurchaseSelections(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ProductTierType? Tier,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] PlanCadenceType? Cadence,
    PasswordManagerSelections? PasswordManager,
    SecretsManagerSelections? SecretsManager,
    string[]? Coupons);
