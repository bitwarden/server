namespace Bit.Core.Models.Mail
{
    public class OrganizationUserAcceptedViewModel : BaseMailModel
    {
        public string OrganizationName { get; set; }
        public string UserEmail { get; set; }
    }
}
