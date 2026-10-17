using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// An entitlement as the partner sees it. Uses the same fields and values as a reconciliation export line, and never
/// carries the bound account's identity.
/// </summary>
public class PartnershipEntitlementResponseModel : ResponseModel
{
    public PartnershipEntitlementResponseModel(OrganizationPartnershipEntitlement entitlement)
        : base("entitlement")
    {
        var record = PartnershipReconciliationExportRecord.From(entitlement);
        ExternalId = record.ExternalId;
        State = JsonNamingPolicy.SnakeCaseLower.ConvertName(record.State.ToString());
        PlanType = JsonNamingPolicy.SnakeCaseLower.ConvertName(record.PlanType.ToString());
        AccountRef = record.AccountRef;
        Metadata = record.Metadata;
        BoundAt = record.BoundAt;
        SuspendedAt = record.SuspendedAt;
        CanceledAt = record.CanceledAt;
        ResumeWindowExpiresAt = record.ResumeWindowExpiresAt;
        LastAppliedAt = record.LastAppliedAt;
        CreatedAt = record.CreatedAt;
        UpdatedAt = record.UpdatedAt;
    }

    /// <param name="result">The provisioning outcome.</param>
    /// <param name="activationLink">A link for the customer to activate the entitlement, when it can be activated.</param>
    public PartnershipEntitlementResponseModel(ProvisionPartnershipEntitlementResult result,
        PartnershipActivationLink? activationLink)
        : this(result.Entitlement)
    {
        Applied = result.Applied;
        AppliedReason = result.AppliedReason;
        ActivationUrl = activationLink?.ActivationUrl;
        ActivationUrlExpiresAt = activationLink?.ExpiresAt;
    }

    /// <param name="result">The suspend, resume, or cancel outcome.</param>
    public PartnershipEntitlementResponseModel(PartnershipEntitlementTransitionResult result)
        : this(result.Entitlement)
    {
        Applied = result.Applied;
        AppliedReason = result.AppliedReason;
        LastAppliedAt = result.LastAppliedAt;
    }

    /// <summary>
    /// The partner's identifier for the customer.
    /// </summary>
    public string ExternalId { get; }
    /// <summary>
    /// One of <c>provisioned</c>, <c>active</c>, <c>suspended</c>, or <c>canceled</c>.
    /// </summary>
    public string State { get; }
    /// <summary>
    /// The sponsored plan, e.g. <c>premium</c>.
    /// </summary>
    public string PlanType { get; }
    /// <summary>
    /// An opaque reference to the binding, present once the customer has activated. Changes if the entitlement is
    /// re-bound to a different account.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? AccountRef { get; }
    /// <summary>
    /// The partner-supplied metadata from the last applied provisioning.
    /// </summary>
    public IDictionary<string, string> Metadata { get; }
    /// <summary>
    /// When the customer activated the entitlement on their account. Absent until then.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? BoundAt { get; }
    /// <summary>
    /// The <c>effectiveAt</c> of the suspension. Cleared when the entitlement is resumed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? SuspendedAt { get; }
    /// <summary>
    /// The <c>effectiveAt</c> of the cancellation. Cleared when the entitlement is resumed or provisioned again.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CanceledAt { get; }
    /// <summary>
    /// For a canceled entitlement, when its held binding is released and it can no longer be resumed or re-activated.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ResumeWindowExpiresAt { get; }
    /// <summary>
    /// The <c>effectiveAt</c> of the last applied change. Requests with an older <c>effectiveAt</c> are acknowledged
    /// but not applied.
    /// </summary>
    public DateTime LastAppliedAt { get; }
    /// <summary>
    /// When the entitlement was first provisioned.
    /// </summary>
    public DateTime CreatedAt { get; }
    /// <summary>
    /// When the entitlement last changed. Use with <c>revisedAfter</c> to sync changes.
    /// </summary>
    public DateTime UpdatedAt { get; }
    /// <summary>
    /// Whether this request changed the entitlement. Only present on write responses.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Applied { get; }
    /// <summary>
    /// Why the request was not applied, e.g. <c>stale_transition</c>. Absent when it was applied or the entitlement
    /// already existed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppliedReason { get; }
    /// <summary>
    /// A short-lived link to send the customer to, present when the entitlement can be activated. Treat it as a secret
    /// until it expires.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActivationUrl { get; }
    /// <summary>
    /// When <c>activationUrl</c> expires.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ActivationUrlExpiresAt { get; }
}
