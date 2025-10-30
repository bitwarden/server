namespace Bit.Core.Models.Mail.Auth;

public class OrganizationWelcomeEmailViewModel : BaseMailModel
{
    public required string OrganizationName { get; set; }
}