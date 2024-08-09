using Bit.Extensions.Hosting;

namespace Bit.Extensions.WebHosting;

public class BitwardenWebHostOptions : BitwardenHostOptions
{
    public bool IncludeRequestLogging { get; set; }
}
