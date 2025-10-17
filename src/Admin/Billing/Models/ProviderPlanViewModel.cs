using Bit.Core.Billing.Providers.Entities;

namespace Bit.Admin.Billing.Models;

public class ProviderPlanViewModel
{
    public string Name { get; set; }
    public int PurchasedSeats { get; set; }
    public int AssignedSeats { get; set; }
    public int UsedSeats { get; set; }
    public int RemainingSeats { get; set; }

    public ProviderPlanViewModel(
        string name,
        ProviderPlan providerPlan,
        int usedSeats)
    {
        var purchasedSeats = (providerPlan.SeatMinimum ?? 0) + (providerPlan.PurchasedSeats ?? 0);

        Name = name;
        PurchasedSeats = purchasedSeats;
        AssignedSeats = providerPlan.AllocatedSeats ?? 0;
        UsedSeats = usedSeats;
        RemainingSeats = purchasedSeats - AssignedSeats;
    }
}
