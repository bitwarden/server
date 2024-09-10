#nullable enable

using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;

namespace Bit.Core.Tools.Models.Business;

/// <summary>
/// Product support monitoring.
/// </summary>
/// <remarks>
/// Do not store secrets in this type.
/// </remarks>
public class ReferenceEvent
{
    /// <summary>
    /// Instantiates a <see cref="ReferenceEvent"/>.
    /// </summary>
    public ReferenceEvent() { }

    /// <inheritdoc cref="ReferenceEvent()" />
    /// <param name="type">Monitored event type.</param>
    /// <param name="source">Entity that created the event.</param>
    /// <param name="currentContext">The conditions in which the event occurred.</param>
    public ReferenceEvent(ReferenceEventType type, IReferenceable source, ICurrentContext currentContext)
    {
        Type = type;
        if (source != null)
        {
            Source = source.IsUser() ? ReferenceEventSource.User : ReferenceEventSource.Organization;
            Id = source.Id;
            ReferenceData = source.ReferenceData;
        }
        if (currentContext != null)
        {
            ClientId = currentContext.ClientId;
            ClientVersion = currentContext.ClientVersion;
        }
    }

    /// <summary>
    /// Monitored event type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReferenceEventType Type { get; set; }

    /// <summary>
    /// The kind of entity that created the event.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReferenceEventSource Source { get; set; }

    /// <inheritdoc cref="IReferenceable.Id"/>
    public Guid Id { get; set; }

    /// <inheritdoc cref="IReferenceable.ReferenceData"/>
    public string? ReferenceData { get; set; }

    /// <summary>
    /// Moment the event occurred.
    /// </summary>
    public DateTime EventDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of users sent invitations by an organization.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.InvitedUsers"/> events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public int? Users { get; set; }

    /// <summary>
    /// Whether or not a subscription was canceled immediately or at the end of the billing period.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when a cancellation occurs immediately.
    /// <see langword="false"/> when a cancellation occurs at the end of a customer's billing period.
    /// Should contain a value only on <see cref="ReferenceEventType.CancelSubscription"/> events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public bool? EndOfPeriod { get; set; }

    /// <summary>
    /// Branded name of the subscription.
    /// </summary>
    /// <value>
    /// Should contain a value only for subscription management events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public string? PlanName { get; set; }

    /// <summary>
    /// Identifies a subscription.
    /// </summary>
    /// <value>
    /// Should contain a value only for subscription management events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public PlanType? PlanType { get; set; }

    /// <summary>
    /// The branded name of the prior plan.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.UpgradePlan"/> events
    /// initiated by organizations.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public string? OldPlanName { get; set; }

    /// <summary>
    /// Identifies the prior plan
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.UpgradePlan"/> events
    /// initiated by organizations.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public PlanType? OldPlanType { get; set; }

    /// <summary>
    /// Seat count when a billable action occurs. When adjusting seats, contains
    /// the new seat count.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.Rebilled"/>,
    /// <see cref="ReferenceEventType.AdjustSeats"/>, <see cref="ReferenceEventType.UpgradePlan"/>,
    /// and <see cref="ReferenceEventType.Signup"/> events initiated by organizations.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public int? Seats { get; set; }

    /// <summary>
    /// Seat count when a seat adjustment occurs.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.AdjustSeats"/>
    /// events initiated by organizations.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public int? PreviousSeats { get; set; }

    /// <summary>
    /// Qty in GB of storage. When adjusting storage, contains the adjusted
    /// storage qty. Otherwise contains the total storage quantity.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.Rebilled"/>,
    /// <see cref="ReferenceEventType.AdjustStorage"/>, <see cref="ReferenceEventType.UpgradePlan"/>,
    /// and <see cref="ReferenceEventType.Signup"/> events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public short? Storage { get; set; }

    /// <summary>
    /// The type of send created or accessed.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.SendAccessed"/>
    /// and <see cref="ReferenceEventType.SendCreated"/> events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SendType? SendType { get; set; }

    /// <summary>
    /// Whether the send has private notes.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when the send has private notes, otherwise <see langword="false"/>.
    /// Should contain a value only on <see cref="ReferenceEventType.SendAccessed"/>
    /// and <see cref="ReferenceEventType.SendCreated"/> events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public bool? SendHasNotes { get; set; }

    /// <summary>
    /// The send expires after its access count exceeds this value.
    /// </summary>
    /// <value>
    /// This field only contains a value when the send has a max access count
    /// and <see cref="Type"/> is <see cref="ReferenceEventType.SendAccessed"/>
    /// or <see cref="ReferenceEventType.SendCreated"/> events.
    /// Otherwise, the value should be <see langword="null"/>.
    /// </value>
    public int? MaxAccessCount { get; set; }

    /// <summary>
    /// Whether the created send has a password.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.SendAccessed"/>
    /// and <see cref="ReferenceEventType.SendCreated"/> events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public bool? HasPassword { get; set; }

    /// <summary>
    /// The administrator that performed the action.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.OrganizationCreatedByAdmin"/>
    /// and <see cref="ReferenceEventType.OrganizationEditedByAdmin"/> events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public string? EventRaisedByUser { get; set; }

    /// <summary>
    /// Whether or not an organization's trial period was started by a sales person.
    /// </summary>
    /// <value>
    /// Should contain a value only on <see cref="ReferenceEventType.OrganizationCreatedByAdmin"/>
    /// and <see cref="ReferenceEventType.OrganizationEditedByAdmin"/> events.
    /// Otherwise the value should be <see langword="null"/>.
    /// </value>
    public bool? SalesAssistedTrialStarted { get; set; }

    /// <summary>
    /// The installation id of the application that originated the event.
    /// </summary>
    /// <value>
    /// <see langword="null"/> when the event was not originated by an application.
    /// </value>
    public string? ClientId { get; set; }

    /// <summary>
    /// The version of the client application that originated the event.
    /// </summary>
    /// <value>
    /// <see langword="null"/> when the event was not originated by an application.
    /// </value>
    public Version? ClientVersion { get; set; }

    /// <summary>
    /// The initiation path of a user who signed up for a paid version of Bitwarden. For example, "Trial from marketing website".
    /// </summary>
    /// <value>
    /// This value should only be populated when the <see cref="ReferenceEventType"/> is <see cref="ReferenceEventType.Signup"/>. Otherwise,
    /// the value should be <see langword="null" />.
    /// </value>
    public string? SignupInitiationPath { get; set; }

    /// <summary>
    /// The upgrade applied to an account. The current plan is listed first,
    /// followed by the plan they are migrating to. For example,
    /// "Teams Starter → Teams, Enterprise".
    /// </summary>
    /// <value>
    /// <see langword="null"/> when the event was not originated by an application,
    /// or when a downgrade occurred.
    /// </value>
    public string? PlanUpgradePath { get; set; }

    /// <summary>
    /// Used for the <see cref="ReferenceEventType.Signup"/> event to determine if the user has opted in to marketing emails.
    /// </summary>
    public bool? ReceiveMarketingEmails { get; set; }

    /// <summary>
    /// Used for the <see cref="ReferenceEventType.SignupEmailClicked"/> event to indicate if the user
    /// landed on the registration finish screen with a valid or invalid email verification token.
    /// </summary>
    public bool? EmailVerificationTokenValid { get; set; }

    /// <summary>
    /// Used for the <see cref="ReferenceEventType.SignupEmailClicked"/> event to indicate if the user
    /// landed on the registration finish screen after re-clicking an already used link.
    /// </summary>
    public bool? UserAlreadyExists { get; set; }
}
