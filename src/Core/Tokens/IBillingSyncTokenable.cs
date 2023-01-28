namespace Bit.Core.Tokens;

public interface IBillingSyncTokenable
{
    public Guid OrganizationId { get; set; }
    public string BillingSyncKey { get; set; }
}
