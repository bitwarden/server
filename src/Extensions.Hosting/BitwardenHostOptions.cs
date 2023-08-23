namespace Bit.Extensions.Hosting;

public class BitwardenHostOptions
{
    public bool IncludeLogging { get; set; } = true;
    public bool IncludeMetrics { get; set; } = true;
}
