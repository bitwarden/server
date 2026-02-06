namespace Bit.Icons.Models;

public class ChangePasswordUriSettings
{
    public virtual bool CacheEnabled { get; set; }
    public virtual int CacheHours { get; set; }
    public virtual long? CacheSizeLimit { get; set; }
}
