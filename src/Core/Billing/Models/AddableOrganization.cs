namespace Bit.Core.Billing.Models;

public record AddableOrganization(
    Guid Id,
    string Name,
    string Plan,
    int Seats,
    bool Disabled = false);
