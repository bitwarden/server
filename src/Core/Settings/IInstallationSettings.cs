namespace Bit.Core.Settings;

public interface IInstallationSettings
{
    public Guid Id { get; set; }
    public string Key { get; set; }
    public string IdentityUri { get; set; }
    public string ApiUri { get; }
}
