namespace Bit.Core.Settings;

public interface IPhishingDomainSettings
{
    string UpdateUrl { get; set; }
    string ChecksumUrl { get; set; }
}
