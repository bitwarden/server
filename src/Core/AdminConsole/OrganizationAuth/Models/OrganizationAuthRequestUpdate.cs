namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class OrganizationAuthRequestUpdate
{
    public Guid Id { get; set; }
    public bool Approved { get; set; }
    public string Key { get; set; }
}
