namespace Bit.Core.Models.Business
{
    public class U2fRegistration
    {
        public string AppId { get; set; }
        public string Challenge { get; set; }
        public string Version { get; set; }
    }
}
