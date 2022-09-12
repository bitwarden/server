namespace Bit.Core.Models.Data;

public class ProviderOrganizationOrganizationDetails
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string Key { get; set; }
    public string Settings { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public int UserCount { get; set; }
    public int? Seats { get; set; }
    public string Plan { get; set; }
}
