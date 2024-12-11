using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Billing.Models.ProcessStripeEvents;

public class EventsFormModel : IValidatableObject
{
    [Required]
    public string EventIds { get; set; }

    [Required]
    [DisplayName("Inspect Only")]
    public bool Inspect { get; set; }

    public List<string> GetEventIds() =>
        EventIds
            ?.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Select(eventId => eventId.Trim())
            .ToList() ?? [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var eventIds = GetEventIds();

        if (eventIds.Any(eventId => !eventId.StartsWith("evt_")))
        {
            yield return new ValidationResult("Event Ids must start with 'evt_'.");
        }
    }
}
