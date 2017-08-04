namespace Bit.Core.Models.Mail
{
    public class BaseMailModel
    {
        private string _webVaultUrl;

        public string SiteName { get; set; }
        public string WebVaultUrl
        {
            get
            {
                return _webVaultUrl;
            }
            set
            {
                _webVaultUrl = string.Concat(value, "/#");
            }
        }
    }
}
