namespace Bit.Core.Models.Data.Integrations;

public class IntegrationConfiguration<T>
{
    public T Configuration { get; set; }
    public string Template { get; set; } = string.Empty;
}
