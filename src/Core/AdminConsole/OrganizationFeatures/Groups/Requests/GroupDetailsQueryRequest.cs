namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Requests;

public class GroupDetailsQueryRequest
{
    public Guid OrganizationId { get; set; }
    public Guid? GroupId { get; set; }
}
