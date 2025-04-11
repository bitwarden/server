namespace Bit.Core.Settings;

public class MailSettings
{
    private ConnectionStringSettings _connectionStringSettings;
    public string ConnectionString
    {
        get => _connectionStringSettings?.ConnectionString;
        set
        {
            if (_connectionStringSettings == null)
            {
                _connectionStringSettings = new ConnectionStringSettings();
            }
            _connectionStringSettings.ConnectionString = value;
        }
    }
    public string ReplyToEmail { get; set; }
    public string AmazonConfigSetName { get; set; }
    public SmtpSettings Smtp { get; set; } = new SmtpSettings();
    public string SendGridApiKey { get; set; }
    public int? SendGridPercentage { get; set; }

    public class SmtpSettings
    {
        public string Host { get; set; }
        public int Port { get; set; } = 25;
        public bool StartTls { get; set; } = false;
        public bool Ssl { get; set; } = false;
        public bool SslOverride { get; set; } = false;
        public string Username { get; set; }
        public string Password { get; set; }
        public bool TrustServer { get; set; } = false;
    }
}

