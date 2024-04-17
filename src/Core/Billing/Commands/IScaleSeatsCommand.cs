using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Commands;

public interface IScaleSeatsCommand
{
    Task ScalePasswordManagerSeats(
        Provider provider,
        PlanType planType,
        int seatAdjustment);
}
