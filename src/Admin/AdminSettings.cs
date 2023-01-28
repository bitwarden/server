namespace Bit.Admin;

public class AdminSettings
{
    public virtual string Admins { get; set; }
    public virtual CloudflareSettings Cloudflare { get; set; }
    public int? DeleteTrashDaysAgo { get; set; }

    public class CloudflareSettings
    {
        public string ZoneId { get; set; }
        public string AuthEmail { get; set; }
        public string AuthKey { get; set; }
    }
}
