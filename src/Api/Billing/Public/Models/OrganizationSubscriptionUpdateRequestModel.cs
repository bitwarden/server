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
    public int? Seats { get; set; }
    public int? Storage { get; set; }
    private int? _maxAutoScaleSeats;
    public int? MaxAutoScaleSeats
    {
        get { return _maxAutoScaleSeats; }
        set { _maxAutoScaleSeats = value < 0 ? null : value; }
    }

    public virtual void ToPasswordManagerSubscriptionUpdate(Organization organization)
    {
        UpdateMaxAutoScaleSeats(organization);

        UpdateSeats(organization);

        UpdateStorage(organization);
    }

    private void UpdateMaxAutoScaleSeats(Organization organization)
    {
        MaxAutoScaleSeats ??= organization.MaxAutoscaleSeats;
    }

    private void UpdateSeats(Organization organization)
    {
        if (Seats is > 0)
        {
            if (organization.Seats.HasValue)
            {
                Seats = Seats.Value - organization.Seats.Value;
            }
        }
        else
        {
            Seats = 0;
        }
    }

    private void UpdateStorage(Organization organization)
    {
        if (Storage is > 0)
        {
            if (organization.MaxStorageGb.HasValue)
            {
                Storage = (short?)(Storage - organization.MaxStorageGb.Value);
            }
        }
        else
        {
            Storage = null;
        }
    }
}

public class SecretsManagerSubscriptionUpdateModel
{
    public int? Seats { get; set; }
    private int? _maxAutoScaleSeats;
    public int? MaxAutoScaleSeats
    {
        get { return _maxAutoScaleSeats; }
        set { _maxAutoScaleSeats = value < 0 ? null : value; }
    }
    public int? ServiceAccounts { get; set; }
    private int? _maxAutoScaleServiceAccounts;
    public int? MaxAutoScaleServiceAccounts
    {
        get { return _maxAutoScaleServiceAccounts; }
        set { _maxAutoScaleServiceAccounts = value < 0 ? null : value; }
    }

    public virtual SecretsManagerSubscriptionUpdate ToSecretsManagerSubscriptionUpdate(Organization organization)
    {
        var update = UpdateUpdateMaxAutoScale(organization);
        UpdateSeats(organization, update);
        UpdateServiceAccounts(organization, update);
        return update;
    }

    private SecretsManagerSubscriptionUpdate UpdateUpdateMaxAutoScale(Organization organization)
    {
        var update = new SecretsManagerSubscriptionUpdate(organization, false)
        {
            MaxAutoscaleSmSeats = MaxAutoScaleSeats ?? organization.MaxAutoscaleSmSeats,
            MaxAutoscaleSmServiceAccounts = MaxAutoScaleServiceAccounts ?? organization.MaxAutoscaleSmServiceAccounts
        };
        return update;
    }

    private void UpdateSeats(Organization organization, SecretsManagerSubscriptionUpdate update)
    {
        if (Seats is > 0)
        {
            if (organization.SmSeats.HasValue)
            {
                Seats = Seats.Value - organization.SmSeats.Value;

            }
            update.AdjustSeats(Seats.Value);
        }
    }

    private void UpdateServiceAccounts(Organization organization, SecretsManagerSubscriptionUpdate update)
    {
        if (ServiceAccounts is > 0)
        {
            if (organization.SmServiceAccounts.HasValue)
            {
                ServiceAccounts = ServiceAccounts.Value - organization.SmServiceAccounts.Value;
            }
            update.AdjustServiceAccounts(ServiceAccounts.Value);
        }
    }
}
