using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Business;

namespace Bit.Api.Billing.Public.Models;

public class OrganizationSubscriptionUpdateRequestModel : IValidatableObject
{
    public PasswordManagerSubscriptionUpdateModel PasswordManager { get; set; }
    public SecretsManagerSubscriptionUpdateModel SecretsManager { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PasswordManager == null && SecretsManager == null)
        {
            yield return new ValidationResult("At least one of PasswordManager or SecretsManager must be provided.");
        }

        yield return ValidationResult.Success;
    }
}

public class PasswordManagerSubscriptionUpdateModel
{
    private int? _seats;
    public int? Seats
    {
        get { return _seats; }
        set { _seats = value is < 0 or null ? 0 : value; }
    }

    private int? _storage;
    public int? Storage
    {
        get { return _storage; }
        set { _storage = value is < 0 or null ? 0 : value; }
    }

    private int? _maxAutoScaleSeats;
    public int? MaxAutoScaleSeats
    {
        get { return _maxAutoScaleSeats; }
        set { _maxAutoScaleSeats = value < 0 ? null : value; }
    }
}

public class SecretsManagerSubscriptionUpdateModel
{
    private int? _seats;
    public int? Seats
    {
        get { return _seats; }
        set { _seats = value is < 0 or null ? 0 : value; }
    }

    private int? _maxAutoScaleSeats;
    public int? MaxAutoScaleSeats
    {
        get { return _maxAutoScaleSeats; }
        set { _maxAutoScaleSeats = value < 0 ? null : value; }
    }

    private int? _serviceAccounts;
    public int? ServiceAccounts
    {
        get { return _serviceAccounts; }
        set { _serviceAccounts = value is < 0 or null ? 0 : value; }
    }

    private int? _maxAutoScaleServiceAccounts;
    public int? MaxAutoScaleServiceAccounts
    {
        get { return _maxAutoScaleServiceAccounts; }
        set { _maxAutoScaleServiceAccounts = value < 0 ? null : value; }
    }

    public virtual SecretsManagerSubscriptionUpdate ToSecretsManagerSubscriptionUpdate(Organization organization)
    {
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            MaxAutoscaleSmSeats = MaxAutoScaleSeats ?? organization.MaxAutoscaleSmSeats,
            MaxAutoscaleSmServiceAccounts = MaxAutoScaleServiceAccounts ?? organization.MaxAutoscaleSmServiceAccounts,
        };
        if (Seats.HasValue)
        {
            update.AdjustSeats(Seats.Value);
        }
        if (ServiceAccounts.HasValue)
        {
            update.AdjustServiceAccounts(ServiceAccounts.Value);
        }

        return update;
    }
}



